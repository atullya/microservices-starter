using NotificationService.Models;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventLog();

// Configure logging levels
builder.Services.Configure<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Warning;
});

// Add logging configuration
builder.Host.ConfigureLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddConsole(options => options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ");
    logging.AddDebug();
});

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Notification Service API",
            Version = "v1",
            Description = "ASP.NET Core microservice for managing notifications"
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Notification Service API",
        Version = "v1",
        Description = "ASP.NET Core microservice for managing notifications",
        Contact = new OpenApiContact
        {
            Name = "Microservices Team",
            Email = "support@microservices.com"
        }
    });
});
builder.Services.AddHealthChecks();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Add request logging middleware
app.Use(async (context, next) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var requestId = Guid.NewGuid().ToString("N")[..8];
    
    logger.LogInformation("[REQ:{requestId}] {method} {path} from {remoteIp}", 
        requestId, context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[REQ:{requestId}] Unhandled exception", requestId);
        throw;
    }
    finally
    {
        stopwatch.Stop();
        logger.LogInformation("[REQ:{requestId}] {method} {path} - {statusCode} in {elapsedMs}ms", 
            requestId, context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
        options.RoutePrefix = string.Empty;
        options.DocumentTitle = "Notification Service API Documentation";
    });
}

app.UseCors("AllowAll");

// Health check endpoint
app.MapHealthChecks("/health").WithName("HealthCheck");

// In-memory list of notifications
var notifications = new List<Notification>();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log service startup
logger.LogInformation("🚀 Notification Service starting up on {timestamp}", DateTime.UtcNow);
logger.LogInformation("📊 Environment: {environment}", app.Environment.EnvironmentName);
logger.LogInformation("🌐 Service URLs: {urls}", string.Join(", ", app.Urls));
logger.LogInformation("📬 Initial notification count: {count}", notifications.Count);

// Get all notifications
app.MapGet("/notifications", (int? skip, int? take, string? status, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 📋 Fetching notifications with skip={skip}, take={take}, status='{status}'", requestId, skip ?? 0, take ?? 10, status ?? "all");
    
    try
    {
        var query = notifications.AsEnumerable();
        
        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(n => n.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            log.LogInformation("[REQ:{requestId}] 🔍 Applied status filter: '{status}'", requestId, status);
        }
        
        var total = query.Count();
        
        // Apply pagination
        var pageSize = take ?? 10;
        var pageSkip = skip ?? 0;
        var result = query.OrderByDescending(n => n.CreatedAt).Skip(pageSkip).Take(pageSize).ToList();
        
        log.LogInformation("[REQ:{requestId}] ✅ Successfully retrieved {count} notifications (total: {total})", requestId, result.Count, total);
        
        return Results.Ok(new
        {
            success = true,
            message = "Notifications retrieved successfully",
            data = result,
            pagination = new { skip = pageSkip, take = pageSize, total }
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error fetching notifications", requestId);
        return Results.Problem("Internal server error while fetching notifications");
    }
})
.WithName("GetNotifications")
.WithOpenApi();

// Get notification by ID
app.MapGet("/notifications/{id}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Fetching notification with id={id}", id);
    
    var notification = notifications.FirstOrDefault(n => n.Id == id);
    if (notification == null)
    {
        log.LogWarning("Notification with id={id} not found", id);
        return Results.NotFound(new
        {
            success = false,
            message = $"Notification with ID {id} not found",
            errors = new[] { "Notification not found" }
        });
    }
    
    return Results.Ok(new
    {
        success = true,
        message = "Notification retrieved successfully",
        data = notification
    });
})
.WithName("GetNotification")
.WithOpenApi();

// Create notification (internal endpoint for other services)
app.MapPost("/notifications", (CreateNotificationRequest request, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 📬 Creating notification: {type} for {recipient}", requestId, request.Type, request.Recipient);
    
    try
    {
        var newNotification = new Notification
        {
            Id = notifications.Count + 1,
            Type = request.Type,
            Recipient = request.Recipient,
            Subject = request.Subject,
            Message = request.Message,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            SentAt = null
        };
        
        notifications.Add(newNotification);
        
        log.LogInformation("[REQ:{requestId}] ✅ Notification created successfully: id={id}, type={type}, recipient={recipient}", 
            requestId, newNotification.Id, newNotification.Type, newNotification.Recipient);
        
        // Simulate sending notification
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Simulate processing time
            newNotification.Status = "Sent";
            newNotification.SentAt = DateTime.UtcNow;
            log.LogInformation("📨 Notification {id} sent successfully to {recipient}", newNotification.Id, newNotification.Recipient);
        });
        
        return Results.Created($"/notifications/{newNotification.Id}", new
        {
            success = true,
            message = "Notification created successfully",
            data = newNotification
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error creating notification: {type} for {recipient}", requestId, request.Type, request.Recipient);
        return Results.Problem("Internal server error while creating notification");
    }
})
.WithName("CreateNotification")
.WithOpenApi();

// Mark notification as read
app.MapPut("/notifications/{id}/read", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Marking notification {id} as read", id);
    
    var notification = notifications.FirstOrDefault(n => n.Id == id);
    if (notification == null)
    {
        log.LogWarning("Notification with id={id} not found", id);
        return Results.NotFound(new
        {
            success = false,
            message = $"Notification with ID {id} not found"
        });
    }
    
    notification.Status = "Read";
    
    log.LogInformation("Notification {id} marked as read", id);
    
    return Results.Ok(new
    {
        success = true,
        message = "Notification marked as read",
        data = notification
    });
})
.WithName("MarkNotificationAsRead")
.WithOpenApi();

// Delete notification
app.MapDelete("/notifications/{id}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Deleting notification with id={id}", id);
    
    var notification = notifications.FirstOrDefault(n => n.Id == id);
    if (notification == null)
    {
        log.LogWarning("Notification with id={id} not found for deletion", id);
        return Results.NotFound(new
        {
            success = false,
            message = $"Notification with ID {id} not found"
        });
    }
    
    notifications.Remove(notification);
    
    log.LogInformation("Notification with id={id} deleted", id);
    
    return Results.Ok(new
    {
        success = true,
        message = "Notification deleted successfully"
    });
})
.WithName("DeleteNotification")
.WithOpenApi();

// Get notification statistics
app.MapGet("/notifications/stats", (ILogger<Program> log) =>
{
    log.LogInformation("Fetching notification statistics");
    
    var stats = notifications
        .GroupBy(n => n.Status)
        .Select(g => new { Status = g.Key, Count = g.Count() })
        .ToList();
    
    return Results.Ok(new
    {
        success = true,
        message = "Statistics retrieved successfully",
        data = new
        {
            Total = notifications.Count,
            ByStatus = stats
        }
    });
})
.WithName("GetNotificationStats")
.WithOpenApi();

app.Run();

// Log service shutdown
logger.LogInformation("🛑 Notification Service shutting down on {timestamp}", DateTime.UtcNow);
logger.LogInformation("📬 Final notification count: {count}", notifications.Count);

public class CreateNotificationRequest
{
    public string Type { get; set; } = string.Empty; // Email, SMS, Push
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
