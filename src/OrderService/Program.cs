    using OrderService.Models;
using OrderService.DTOs;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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

// Get all orders with pagination
app.MapGet("/orders", (int? skip, int? take, ILogger<Program> log) =>
{
    log.LogInformation("Fetching orders with skip={skip}, take={take}", skip ?? 0, take ?? 10);
    
    var pageSize = take ?? 10;
    var pageSkip = skip ?? 0;
    
    var total = orders.Count;
    var result = orders.Skip(pageSkip).Take(pageSize).ToList();
    
    return Results.Ok(new
    {
        success = true,
        message = "Orders retrieved successfully",
        data = result,
        pagination = new { skip = pageSkip, take = pageSize, total }
    });
})
.WithName("GetOrders")
.WithOpenApi();

// Get order by ID
app.MapGet("/orders/{id}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Fetching order with id={id}", id);
    
    var order = orders.FirstOrDefault(o => o.Id == id);
    if (order == null)
    {
        log.LogWarning("Order with id={id} not found", id);
        return Results.NotFound(new ApiResponse
        {
            Success = false,
            Message = $"Order with ID {id} not found",
            Errors = new() { "Order not found" }
        });
    }
    
    return Results.Ok(new ApiResponse<Order>
    {
        Success = true,
        Message = "Order retrieved successfully",
        Data = order
    });
})
.WithName("GetOrder")
.WithOpenApi();

// Create order
app.MapPost("/orders", async (CreateOrderRequest request, HttpClient httpClient, ILogger<Program> log) =>
{
    log.LogInformation("Creating order for product={productId}, customer={customer}", request.ProductId, request.CustomerName);
    
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
        log.LogWarning("Order creation validation failed: {errors}", string.Join(", ", errors));
        return Results.BadRequest(new ApiResponse
        {
            Success = false,
            Message = "Validation failed",
            Errors = errors
        });
    }
    
    try
    {
        log.LogInformation("Calling ProductService to get product details for productId={productId}", request.ProductId);
        
        // Call ProductService to get product details
        var productResponse = await httpClient.GetAsync($"http://product-service:8080/products/{request.ProductId}");
        
        if (!productResponse.IsSuccessStatusCode)
        {
            log.LogWarning("ProductService returned status {code} for productId={productId}", productResponse.StatusCode, request.ProductId);
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
            log.LogWarning("Failed to deserialize product from ProductService response");
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Product with ID {request.ProductId} not found"
            });
        }
        
        log.LogInformation("Product retrieved: name={name}, price={price}", product.Name, product.Price);
        
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
        log.LogInformation("Order created with id={id}, totalPrice={price}", order.Id, order.TotalPrice);
        
        return Results.Created($"/orders/{order.Id}", new ApiResponse<Order>
        {
            Success = true,
            Message = "Order created successfully",
            Data = order
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Error creating order");
        return Results.StatusCode(500);
    }
})
.WithName("CreateOrder")
.WithOpenApi();

// Cancel order
app.MapDelete("/orders/{id}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Cancelling order with id={id}", id);
    
    var order = orders.FirstOrDefault(o => o.Id == id);
    if (order == null)
    {
        log.LogWarning("Order with id={id} not found for cancellation", id);
        return Results.NotFound(new ApiResponse
        {
            Success = false,
            Message = $"Order with ID {id} not found"
        });
    }
    
    order.Status = "Cancelled";
    log.LogInformation("Order with id={id} cancelled", id);
    
    return Results.Ok(new ApiResponse<Order>
    {
        Success = true,
        Message = "Order cancelled successfully",
        Data = order
    });
})
.WithName("CancelOrder")
.WithOpenApi();
    app.Run();

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
    }
