using System.Reflection;
using System.Threading.RateLimiting;
using Anthropic;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PhotoLib API", Version = "v1" });

    // include the XML comments file (generated when GenerateDocumentationFile=true in .csproj)
    var xmlFile = "backend.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    }
});

var conn = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<PhotoDbContext>(options =>
    options.UseSqlite(conn));

builder.Services.AddScoped<TagResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<GalleryAccessService>();

// Session cookie for signed-in users. API-only backend, so the default
// MVC behavior (302 redirect to a login page on an unauthenticated
// [Authorize] request) is overridden to return a bare status code instead -
// there's no server-rendered login page to redirect to, and the frontend
// needs a clean 401/403 to react to.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// Config-driven and stateless once built, so a single shared instance is safe.
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var photosRoot = Path.Combine(
        Directory.GetCurrentDirectory(),
        configuration["Storage:PhotosPath"]!);

    return new PhotoFilePathHelper(photosRoot);
});
builder.Services.AddScoped<PhotoImageProcessingService>();

// Config-driven and stateless once built, so a single shared instance is safe
// (same rationale as the PhotoFilePathHelper registration above). The key is
// read from user-secrets in development (`dotnet user-secrets set
// "Anthropic:ApiKey" "..."`) - never committed to appsettings.json.
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new AnthropicClient { ApiKey = configuration["Anthropic:ApiKey"] };
});
builder.Services.AddScoped<PhotoDescriptionAiService>();

builder.Services.AddHttpClient();

// Stopgap abuse guard on the AI endpoint (see TODO,md "Immediate risk"):
// there's no per-user auth/quota yet, so if a single hosted instance is
// shared with others, one client could otherwise drain the shared
// Anthropic key with unlimited requests. Keyed by IP rather than global so
// one noisy client doesn't lock everyone else out on a shared instance.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AiGeneration", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));

    // Same per-IP fixed-window shape as AiGeneration above, sized for
    // credential-stuffing/brute-force protection now that real account
    // passwords exist.
    options.AddPolicy("AuthLogin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            }));

    // Stricter: a successful guess of the admin key lets an attacker mint
    // arbitrary accounts, not just log into one.
    options.AddPolicy("AuthAdminRegister", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
});

// Cross-origin access for the "Add from internet" bookmarklet: the bookmarklet
// runs on arbitrary third-party pages and posts an image URL directly to this
// API, so that one endpoint needs to accept requests from any origin. Every
// other endpoint stays same-origin-only (no policy applied to them).
builder.Services.AddCors(options =>
{
    options.AddPolicy("BookmarkletUpload", policy =>
        policy.AllowAnyOrigin()
              .WithMethods("POST")
              .AllowAnyHeader());
});

var app = builder.Build();

// Applies any pending EF Core migrations on startup, so a fresh container
// (or a fresh clone) doesn't need a separate manual `dotnet ef database
// update` step - the database file is created/updated the first time the
// app actually runs.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<PhotoDbContext>().Database.Migrate();
}

// Only enable swagger UI in Development by default (you can enable always if you prefer)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();        // generates swagger.json at /swagger/v1/swagger.json
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhotoLib API v1");
        c.RoutePrefix = "swagger"; // open UI at /swagger
    });
}

app.UseRouting();

// Chrome's Private Network Access policy blocks a public page (e.g. any
// https:// site) from fetching a loopback address like localhost unless the
// preflight response explicitly allows it. ASP.NET Core's CORS middleware
// doesn't set this header yet, so it's added here for any such preflight.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsOptions(context.Request.Method) &&
        context.Request.Headers.TryGetValue("Access-Control-Request-Private-Network", out var pna) &&
        pna == "true")
    {
        context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
    }

    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();
