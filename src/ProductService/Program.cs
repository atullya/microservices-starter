using ProductService.Models;
using ProductService.DTOs;
using Microsoft.OpenApi.Models;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/products", (int? skip, int? take, string? search, ILogger<Program> log) =>
{
    log.LogInformation("Fetching products with skip={skip}, take={take}, search={search}", skip ?? 0, take ?? 10, search ?? "all");
    
    var query = products.AsEnumerable();
    
    // Apply search filter
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
    }
    
    var total = query.Count();
    
    // Apply pagination
    var pageSize = take ?? 10;
    var pageSkip = skip ?? 0;
    var result = query.Skip(pageSkip).Take(pageSize).ToList();
    
    return Results.Ok(new
    {
        success = true,
        message = "Products retrieved successfully",
        data = result,
        pagination = new { skip = pageSkip, take = pageSize, total }
    });
})
.WithName("GetProducts")
.WithOpenApi();

app.MapGet("/products/{id}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Fetching product with id={id}", id);
    
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product == null)
    {
        log.LogWarning("Product with id={id} not found", id);
        return Results.NotFound(new ApiResponse
        {
            Success = false,
            Message = $"Product with ID {id} not found",
            Errors = new() { "Product not found" }
        });
    }
    
    return Results.Ok(new ApiResponse<Product>
    {
        Success = true,
        Message = "Product retrieved successfully",
        Data = product
    });
})
.WithName("GetProduct")
.WithOpenApi();

app.MapPost("/products", (CreateProductRequest request, ILogger<Program> log) =>
{
    log.LogInformation("Creating product: {name}", request.Name);
    
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
        log.LogWarning("Product creation validation failed: {errors}", string.Join(", ", errors));
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
    log.LogInformation("Product created with id={id}", newProduct.Id);
    
    return Results.Created($"/products/{newProduct.Id}", new ApiResponse<Product>
    {
        Success = true,
        Message = "Product created successfully",
        Data = newProduct
    });
})
.WithName("CreateProduct")
.WithOpenApi();

app.MapPut("/products/{id}", (int id, CreateProductRequest request, ILogger<Program> log) =>
{
    log.LogInformation("Updating product with id={id}", id);
    
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product == null)
    {
        log.LogWarning("Product with id={id} not found for update", id);
        return Results.NotFound(new ApiResponse
        {
            Success = false,
            Message = $"Product with ID {id} not found",
            Errors = new() { "Product not found" }
        });
    }
    
    // Validation
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(request.Name))
        errors.Add("Product name is required");
    if (request.Price <= 0)
        errors.Add("Price must be greater than 0");
    
    if (errors.Any())
    {
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
    
    log.LogInformation("Product with id={id} updated", id);
    
    return Results.Ok(new ApiResponse<Product>
    {
        Success = true,
        Message = "Product updated successfully",
        Data = product
    });
})
.WithName("UpdateProduct")
.WithOpenApi();

app.MapDelete("/products/{id}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Deleting product with id={id}", id);
    
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product == null)
    {
        log.LogWarning("Product with id={id} not found for deletion", id);
        return Results.NotFound(new ApiResponse
        {
            Success = false,
            Message = $"Product with ID {id} not found"
        });
    }
    
    products.Remove(product);
    log.LogInformation("Product with id={id} deleted", id);
    
    return Results.Ok(new ApiResponse
    {
        Success = true,
        Message = "Product deleted successfully"
    });
})
.WithName("DeleteProduct")
.WithOpenApi();

app.Run();
