using ProductService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// In-memory list of products (for simplicity)
var products = new List<Product>
{
    new Product { Id = 1, Name = "Laptop", Price = 999.99m, Description = "A powerful laptop" },
    new Product { Id = 2, Name = "Mouse", Price = 19.99m, Description = "Wireless mouse" }
};

app.MapGet("/products", () => products)
    .WithName("GetProducts");

app.MapGet("/products/{id}", (int id) => products.FirstOrDefault(p => p.Id == id))
    .WithName("GetProduct");

app.MapPost("/products", (Product product) =>
{
    product.Id = products.Max(p => p.Id) + 1;
    products.Add(product);
    return Results.Created($"/products/{product.Id}", product);
})
.WithName("CreateProduct");

app.Run();
