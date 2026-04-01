    using OrderService.Models;
    using System.Net.Http.Json;

    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddHttpClient();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    // In-memory list of orders
    var orders = new List<Order>();

    app.MapGet("/orders", () => orders)
        .WithName("GetOrders");

    app.MapPost("/orders", async (Order order, HttpClient httpClient) =>
    {
        // Call ProductService to get product price
        var productResponse = await httpClient.GetAsync("http://product-service:8080/products/" + order.ProductId);
        if (!productResponse.IsSuccessStatusCode)
        {
            return Results.NotFound("Product not found");
        }
        var product = await productResponse.Content.ReadFromJsonAsync<Product>();
        if (product == null)
        {
            return Results.NotFound("Product not found");
        }

        order.TotalPrice = product.Price * order.Quantity;
        order.OrderDate = DateTime.Now;
        order.Id = orders.Count + 1;
        orders.Add(order);
        return Results.Created($"/orders/{order.Id}", order);
    })
    .WithName("CreateOrder");

    app.Run();

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
    }
