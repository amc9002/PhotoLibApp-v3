using System.Reflection;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;

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
app.UseAuthorization();
app.MapControllers();
app.Run();
