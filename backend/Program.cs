using System.Reflection;
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

builder.Services.AddHttpClient();

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
app.UseAuthorization();
app.MapControllers();
app.Run();
