using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Add services to the container.
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("AllowAll");

// Add middleware BEFORE Ocelot to handle non-Ocelot routes
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Handle /gateway/status
    if (path == "/gateway/status")
    {
        logger.LogInformation("Gateway status requested");
        context.Response.ContentType = "application/json";
        var response = new { status = "running", service = "API Gateway", timestamp = DateTime.UtcNow };
        await context.Response.WriteAsJsonAsync(response);
        return;
    }
    
    // Handle /health
    if (path == "/health")
    {
        logger.LogInformation("Health check requested");
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status = "healthy" });
        return;
    }
    
    // Log request
    logger.LogInformation("API Gateway request: {method} {path}", context.Request.Method, path);
    
    await next.Invoke();
    
    logger.LogInformation("API Gateway response: {method} {path} - Status: {statusCode}", 
        context.Request.Method, path, context.Response.StatusCode);
});

// Configure Ocelot
await app.UseOcelot();

app.Run();
