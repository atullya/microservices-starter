using ProductService.Models;
using ProductService.DTOs;
using Microsoft.OpenApi.Models;
using System.ComponentModel.DataAnnotations;

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
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Product Service API",
            Version = "v1",
            Description = "ASP.NET Core microservice for managing products"
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "ASP.NET Core microservice for managing products",
        Contact = new OpenApiContact
        {
            Name = "Microservices Team",
            Email = "support@microservices.com"
        }
    });
});
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
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Service API v1");
        options.RoutePrefix = string.Empty;
        options.DocumentTitle = "Product Service API Documentation";
    });
}

app.UseCors("AllowAll");

// Health check endpoint
app.MapHealthChecks("/health").WithName("HealthCheck");

// In-memory list of products
var products = new List<Product>
{
    new Product { Id = 1, Name = "Laptop", Price = 999.99m, Description = "High-performance laptop", Stock = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
    new Product { Id = 2, Name = "Mouse", Price = 19.99m, Description = "Wireless mouse", Stock = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
    new Product { Id = 3, Name = "Keyboard", Price = 79.99m, Description = "Mechanical keyboard", Stock = 25, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
};

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log service startup
logger.LogInformation("🚀 Product Service starting up on {timestamp}", DateTime.UtcNow);
logger.LogInformation("📊 Environment: {environment}", app.Environment.EnvironmentName);
logger.LogInformation("🌐 Service URLs: {urls}", string.Join(", ", app.Urls));
logger.LogInformation("📦 Initial product count: {count}", products.Count);

app.MapGet("/products", (int? skip, int? take, string? search, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 📋 Fetching products with skip={skip}, take={take}, search='{search}'", requestId, skip ?? 0, take ?? 10, search ?? "all");
    
    try
    {
        var query = products.AsEnumerable();
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                    p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            log.LogInformation("[REQ:{requestId}] 🔍 Applied search filter for term: '{search}'", requestId, search);
        }
        
        var total = query.Count();
        
        // Apply pagination
        var pageSize = take ?? 10;
        var pageSkip = skip ?? 0;
        var result = query.Skip(pageSkip).Take(pageSize).ToList();
        
        log.LogInformation("[REQ:{requestId}] ✅ Successfully retrieved {count} products (total: {total})", requestId, result.Count, total);
        
        return Results.Ok(new
        {
            success = true,
            message = "Products retrieved successfully",
            data = result,
            pagination = new { skip = pageSkip, take = pageSize, total }
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error fetching products", requestId);
        return Results.Problem("Internal server error while fetching products");
    }
})
.WithName("GetProducts")
.WithOpenApi();

app.MapGet("/products/{id}", (int id, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 🔍 Fetching product with id={id}", requestId, id);
    
    try
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Product with id={id} not found", requestId, id);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Product with ID {id} not found",
                Errors = new() { "Product not found" }
            });
        }
        
        log.LogInformation("[REQ:{requestId}] ✅ Successfully retrieved product: {name} (price: {price:C}, stock: {stock})", 
            requestId, product.Name, product.Price, product.Stock);
        
        return Results.Ok(new ApiResponse<Product>
        {
            Success = true,
            Message = "Product retrieved successfully",
            Data = product
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error fetching product {id}", requestId, id);
        return Results.Problem("Internal server error while fetching product");
    }
})
.WithName("GetProduct")
.WithOpenApi();

app.MapPost("/products", (CreateProductRequest request, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] ➕ Creating product: {name}", requestId, request.Name);
    
    try
    {
        // Validation
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Product name is required");
        if (request.Price <= 0)
            errors.Add("Price must be greater than 0");
        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add("Description is required");
        
        if (errors.Any())
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Product creation validation failed: {errors}", requestId, string.Join(", ", errors));
            return Results.BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Validation failed",
                Errors = errors
            });
        }
        
        var newProduct = new Product
        {
            Id = products.Max(p => p.Id) + 1,
            Name = request.Name,
            Price = request.Price,
            Description = request.Description,
            Stock = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        products.Add(newProduct);
        
        log.LogInformation("[REQ:{requestId}] ✅ Product created successfully: id={id}, name={name}, price={price:C}", 
            requestId, newProduct.Id, newProduct.Name, newProduct.Price);
        
        return Results.Created($"/products/{newProduct.Id}", new ApiResponse<Product>
        {
            Success = true,
            Message = "Product created successfully",
            Data = newProduct
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error creating product: {name}", requestId, request.Name);
        return Results.Problem("Internal server error while creating product");
    }
})
.WithName("CreateProduct")
.WithOpenApi();

app.MapPut("/products/{id}", (int id, CreateProductRequest request, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] ✏️ Updating product with id={id}", requestId, id);
    
    try
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Product with id={id} not found for update", requestId, id);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Product with ID {id} not found",
                Errors = new() { "Product not found" }
            });
        }
        
        // Log previous state
        var previousName = product.Name;
        var previousPrice = product.Price;
        
        // Validation
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Product name is required");
        if (request.Price <= 0)
            errors.Add("Price must be greater than 0");
        
        if (errors.Any())
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Product update validation failed: {errors}", requestId, string.Join(", ", errors));
            return Results.BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Validation failed",
                Errors = errors
            });
        }
        
        product.Name = request.Name;
        product.Price = request.Price;
        product.Description = request.Description;
        product.UpdatedAt = DateTime.UtcNow;
        
        log.LogInformation("[REQ:{requestId}] ✅ Product {id} updated: name '{oldName}' → '{newName}', price {oldPrice:C} → {newPrice:C}", 
            requestId, product.Id, previousName, product.Name, previousPrice, product.Price);
        
        return Results.Ok(new ApiResponse<Product>
        {
            Success = true,
            Message = "Product updated successfully",
            Data = product
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error updating product {id}", requestId, id);
        return Results.Problem("Internal server error while updating product");
    }
})
.WithName("UpdateProduct")
.WithOpenApi();

app.MapDelete("/products/{id}", (int id, ILogger<Program> log) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    log.LogInformation("[REQ:{requestId}] 🗑️ Deleting product with id={id}", requestId, id);
    
    try
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            log.LogWarning("[REQ:{requestId}] ⚠️ Product with id={id} not found for deletion", requestId, id);
            return Results.NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Product with ID {id} not found"
            });
        }
        
        var productName = product.Name;
        products.Remove(product);
        
        log.LogInformation("[REQ:{requestId}] ✅ Product '{name}' (id: {id}) deleted successfully", requestId, productName, id);
        
        return Results.Ok(new ApiResponse
        {
            Success = true,
            Message = "Product deleted successfully"
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "[REQ:{requestId}] ❌ Error deleting product {id}", requestId, id);
        return Results.Problem("Internal server error while deleting product");
    }
})
.WithName("DeleteProduct")
.WithOpenApi();

app.Run();

// Log service shutdown
logger.LogInformation("🛑 Product Service shutting down on {timestamp}", DateTime.UtcNow);
logger.LogInformation("📊 Final product count: {count}", products.Count);
