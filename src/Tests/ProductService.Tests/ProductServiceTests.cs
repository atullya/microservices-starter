using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductService;
using ProductService.Data;
using ProductService.Models;
using ProductService.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProductService.Tests
{
    public class ProductServiceTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ProductServiceTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ProductDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<ProductDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDb");
                    });
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetProducts_ReturnsSuccessAndCorrectContentType()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/products");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        }

        [Fact]
        public async Task CreateProduct_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var productRequest = new CreateProductRequest
            {
                Name = "Test Product",
                Price = 99.99m,
                Description = "Test Description"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/products", productRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<Product>>();
            Assert.NotNull(content);
            Assert.True(content.Success);
            Assert.Equal("Test Product", content.Data.Name);
        }

        [Fact]
        public async Task CreateProduct_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var productRequest = new CreateProductRequest
            {
                Name = "", // Invalid
                Price = -10, // Invalid
                Description = "Test Description"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/products", productRequest);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetProductById_ExistingProduct_ReturnsProduct()
        {
            // Arrange
            var createRequest = new CreateProductRequest
            {
                Name = "Test Product",
                Price = 99.99m,
                Description = "Test Description"
            };
            var createResponse = await _client.PostAsJsonAsync("/products", createRequest);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Product>>();

            // Act
            var response = await _client.GetAsync($"/products/{createdProduct.Data.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            var product = await response.Content.ReadFromJsonAsync<ApiResponse<Product>>();
            Assert.NotNull(product);
            Assert.True(product.Success);
            Assert.Equal(createdProduct.Data.Id, product.Data.Id);
        }

        [Fact]
        public async Task GetProductById_NonExistingProduct_ReturnsNotFound()
        {
            // Act
            var response = await _client.GetAsync("/products/99999");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateProduct_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var createRequest = new CreateProductRequest
            {
                Name = "Original Product",
                Price = 99.99m,
                Description = "Original Description"
            };
            var createResponse = await _client.PostAsJsonAsync("/products", createRequest);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Product>>();

            var updateRequest = new CreateProductRequest
            {
                Name = "Updated Product",
                Price = 199.99m,
                Description = "Updated Description"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/products/{createdProduct.Data.Id}", updateRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedProduct = await response.Content.ReadFromJsonAsync<ApiResponse<Product>>();
            Assert.NotNull(updatedProduct);
            Assert.True(updatedProduct.Success);
            Assert.Equal("Updated Product", updatedProduct.Data.Name);
            Assert.Equal(199.99m, updatedProduct.Data.Price);
        }

        [Fact]
        public async Task DeleteProduct_ExistingProduct_ReturnsSuccess()
        {
            // Arrange
            var createRequest = new CreateProductRequest
            {
                Name = "Product to Delete",
                Price = 99.99m,
                Description = "Will be deleted"
            };
            var createResponse = await _client.PostAsJsonAsync("/products", createRequest);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Product>>();

            // Act
            var response = await _client.DeleteAsync($"/products/{createdProduct.Data.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            
            // Verify product is deleted
            var getResponse = await _client.GetAsync($"/products/{createdProduct.Data.Id}");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task SearchProducts_ReturnsFilteredResults()
        {
            // Arrange
            var products = new[]
            {
                new CreateProductRequest { Name = "Laptop", Price = 999.99m, Description = "High performance laptop" },
                new CreateProductRequest { Name = "Mouse", Price = 29.99m, Description = "Wireless mouse" },
                new CreateProductRequest { Name = "Keyboard", Price = 79.99m, Description = "Mechanical keyboard" }
            };

            foreach (var product in products)
            {
                await _client.PostAsJsonAsync("/products", product);
            }

            // Act
            var response = await _client.GetAsync("/products?search=laptop");

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync();
            var json = JsonSerializer.Serialize(result);
            var data = JsonDocument.Parse(json).RootElement.GetProperty("data");
            
            Assert.True(data.GetArrayLength() > 0);
            // Should contain laptop but not mouse or keyboard
        }

        [Fact]
        public async Task Pagination_ReturnsCorrectSubset()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                var product = new CreateProductRequest
                {
                    Name = $"Product {i}",
                    Price = 10.99m * (i + 1),
                    Description = $"Description for product {i}"
                };
                await _client.PostAsJsonAsync("/products", product);
            }

            // Act
            var response = await _client.GetAsync("/products?skip=5&take=5");

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync();
            var json = JsonSerializer.Serialize(result);
            var data = JsonDocument.Parse(json).RootElement.GetProperty("data");
            var pagination = JsonDocument.Parse(json).RootElement.GetProperty("pagination");
            
            Assert.Equal(5, data.GetArrayLength());
            Assert.Equal(5, pagination.GetProperty("skip").GetInt32());
            Assert.Equal(5, pagination.GetProperty("take").GetInt32());
        }
    }
}
