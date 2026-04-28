    using OrderService.Models;
using OrderService.DTOs;
using System.Net.Http.Json;

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

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();
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
    app.MapOpenApi();
}

app.UseCors("AllowAll");

// Health check endpoint
app.MapHealthChecks("/health").WithName("HealthCheck");

// In-memory list of orders
var orders = new List<Order>();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log service startup
logger.LogInformation("🚀 Order Service starting up on {timestamp}", DateTime.UtcNow);
logger.LogInformation("📊 Environment: {environment}", app.Environment.EnvironmentName);
logger.LogInformation("🌐 Service URLs: {urls}", string.Join(", ", app.Urls));

// Get all orders with pagination
app.MapGet("/orders", (int? skip, int? take, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 📋 Fetching orders with skip={skip}, take={take}", requestId, skip ?? 0, take ?? 10);
    
    try
    {
        var pageSize = take ?? 10;
        var pageSkip = skip ?? 0;
        
        var total = orders.Count;
        var result = orders.Skip(pageSkip).Take(pageSize).ToList();
        
        log.LogInformation("[REQ:{requestId}] ✅ Successfully retrieved {count} orders (total: {total})", requestId, result.Count, total);
        
        return Results.Ok(new
        {
            success = true,
            message = "Orders retrieved successfully",
            data = result,
            pagination = new { skip = pageSkip, take = pageSize, total }
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error fetching orders", requestId);
        return Results.Problem("Internal server error while fetching orders");
    }
})
.WithName("GetOrders")
.WithOpenApi();

// Get order by ID
app.MapGet("/orders/{id}", (int id, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 🔍 Fetching order with id={id}", requestId, id);
    
    try
    {
        var order = orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Order with id={id} not found", requestId, id);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Order with ID {id} not found",
                Errors = new() { "Order not found" }
            });
        }
        
        log.LogInformation("[REQ:{requestId}] ✅ Successfully retrieved order {id} for customer {customer}", requestId, order.Id, order.CustomerName);
        
        return Results.Ok(new ApiResponse<Order>
        {
            Success = true,
            Message = "Order retrieved successfully",
            Data = order
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error fetching order {id}", requestId, id);
        return Results.Problem("Internal server error while fetching order");
    }
})
.WithName("GetOrder")
.WithOpenApi();

// Create order
app.MapPost("/orders", async (CreateOrderRequest request, HttpClient httpClient, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 🛒 Creating order for product={productId}, customer={customer}, quantity={qty}", 
        requestId, request.ProductId, request.CustomerName, request.Quantity);
    
    try
    {
        // Validation
        var errors = new List<string>();
        if (request.ProductId <= 0)
            errors.Add("Product ID must be greater than 0");
        if (request.Quantity <= 0)
            errors.Add("Quantity must be greater than 0");
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            errors.Add("Customer name is required");
        
        if (errors.Any())
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Order creation validation failed: {errors}", requestId, string.Join(", ", errors));
            return Results.BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Validation failed",
                Errors = errors
            });
        }
        
        log.LogInformation("[REQ:{requestId}] 🔗 Calling ProductService to get product details for productId={productId}", requestId, request.ProductId);
        // Call ProductService to get product details
        var productResponse = await httpClient.GetAsync($"http://product-service:8080/products/{request.ProductId}");
        
        if (!productResponse.IsSuccessStatusCode)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ ProductService returned status {code} for productId={productId}", requestId, productResponse.StatusCode, request.ProductId);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Product with ID {request.ProductId} not found",
                Errors = new() { "Product not found in ProductService" }
            });
        }
        
        var responseContent = await productResponse.Content.ReadAsStringAsync();
        var parsedResponse = System.Text.Json.JsonDocument.Parse(responseContent);
        var productDataElement = parsedResponse.RootElement.GetProperty("data");
        var product = System.Text.Json.JsonSerializer.Deserialize<Product>(productDataElement.GetRawText());
        
        if (product == null)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Failed to deserialize product from ProductService response", requestId);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Product with ID {request.ProductId} not found"
            });
        }
        
        log.LogInformation("[REQ:{requestId}] ✅ Product retrieved: name={name}, price={price}", requestId, product.Name, product.Price);
        // Create order
        var order = new Order
        {
            Id = orders.Count + 1,
            ProductId = request.ProductId,
            ProductName = product.Name,
            ProductPrice = product.Price,
            Quantity = request.Quantity,
            TotalPrice = product.Price * request.Quantity,
            CustomerName = request.CustomerName,
            OrderDate = DateTime.UtcNow,
            Status = "Confirmed"
        };
        
        orders.Add(order);
        
        log.LogInformation("[REQ:{requestId}] ✅ Order created successfully: id={id}, total={price:C}, status={status}", 
            requestId, order.Id, order.TotalPrice, order.Status);
        
        return Results.Created($"/orders/{order.Id}", new ApiResponse<Order>
        {
            Success = true,
            Message = "Order created successfully",
            Data = order
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error creating order for product={productId}, customer={customer}", 
            requestId, request.ProductId, request.CustomerName);
        return Results.StatusCode(500);
    }
})
.WithName("CreateOrder")
.WithOpenApi();

// Cancel order
app.MapDelete("/orders/{id}", (int id, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] ❌ Cancelling order with id={id}", requestId, id);
    
    try
    {
        var order = orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Order with id={id} not found for cancellation", requestId, id);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Order with ID {id} not found"
            });
        }
        
        var previousStatus = order.Status;
        order.Status = "Cancelled";
        
        log.LogInformation("[REQ:{requestId}] ✅ Order {id} status changed from {oldStatus} to {newStatus}", 
            requestId, order.Id, previousStatus, order.Status);
        
        return Results.Ok(new ApiResponse<Order>
        {
            Success = true,
            Message = "Order cancelled successfully",
            Data = order
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error cancelling order {id}", requestId, id);
        return Results.Problem("Internal server error while cancelling order");
    }
})
.WithName("CancelOrder")
.WithOpenApi();
    app.Run();

// Log service shutdown
logger.LogInformation("🛑 Order Service shutting down on {timestamp}", DateTime.UtcNow);

public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
    }
