# Advanced ASP.NET Core Microservices Architecture Guide

## 📚 Table of Contents
1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [New Features Added](#new-features-added)
4. [Detailed Feature Breakdown](#detailed-feature-breakdown)
5. [API Documentation](#api-documentation)
6. [Running the Project](#running-the-project)
7. [Production Considerations](#production-considerations)
8. [Common Challenges & Solutions](#common-challenges--solutions)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

---

## Project Overview

This is a **production-grade microservices application** demonstrating industry best practices in building scalable, maintainable systems using ASP.NET Core. The project includes:

- **Product Service**: Manages product catalog with CRUD operations
- **Order Service**: Handles order processing with inter-service communication
- **API Gateway**: Routes requests and handles cross-cutting concerns
- **Frontend**: Interactive UI for testing the services
- **Docker**: Containerization and orchestration

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Frontend (Nginx)                        │
│                    Port 8080 (Browser)                       │
└─┬─────────────────────────────────────────────────────────┬─┘
  │                                                          │
  └───────────────────────────────────────────────────────┐  │
                                                            │  │
┌────────────────────────────────────────────────────────┐ │  │
│           API Gateway (Ocelot)                          │ │  │
│           Port 5002 / 8080 (in Docker)                  │ │  │
│  - Request/Response Logging                             │ │  │
│  - Health Checks                                        │ │  │
│  - CORS Handling                                        │ │  │
└────────────────────────────────────────────────────────┘ │  │
       │                                │                    │  │
       │ /api/products                  │ /api/orders        │  │
       │ /api/products/{id}             │ /api/orders/{id}   │  │
       ▼                                ▼                    │  │
┌──────────────────┐          ┌──────────────────┐         │  │
│ Product Service  │          │  Order Service   │         │  │
│  Port 5000       │          │   Port 5001      │         │  │
│ /products        │  HTTP    │  /orders         │         │  │
│ /health          │◄─────────▶  /health         │         │  │
│                  │ (calls)  │                  │         │  │
└──────────────────┘          └──────────────────┘         │  │
       ▲                              ▲                     │  │
       │                              │                     │  │
       └──────────────────────────────┴─────────────────────┘  │
                                                               │
                              Browser ────────────────────────┘
```

---

## New Features Added

### 1. **Data Transfer Objects (DTOs)**
- Decouples API contracts from internal models
- Provides better validation and documentation
- Located in: `DTOs/` folders of each service

### 2. **Structured Logging**
```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
```
- Console output with timestamps and log levels
- Information tracking for every operation
- Error logging for debugging

### 3. **Health Checks**
```csharp
builder.Services.AddHealthChecks();
app.MapHealthChecks("/health");
```
- `/health` endpoint on each service
- Allows monitoring and orchestration
- Essential for container orchestration (Kubernetes)

### 4. **Comprehensive Validation**
- Request validation before processing
- Detailed error messages
- Returns structured error responses

### 5. **Pagination & Filtering**
- `GET /products?skip=0&take=10&search=laptop`
- Reduces payload size
- Improves performance with large datasets

### 6. **Standardized API Responses**
```csharp
{
  "success": true,
  "message": "Operation successful",
  "data": { /* actual data */ },
  "errors": []
}
```

### 7. **CRUD Operations**
- **Product Service**: Create, Read, Update, Delete products
- **Order Service**: Create, Read, Cancel orders

### 8. **Inter-Service Communication**
- OrderService calls ProductService to fetch product details
- Proper error handling for service failures
- Asynchronous HTTP calls

### 9. **Modern Frontend UI**
- Real-time status monitoring
- Interactive product and order management
- Auto-refresh functionality
- Responsive design

### 10. **Docker Support**
- Containerized services
- Docker Compose orchestration
- Environment variable configuration

---

## Detailed Feature Breakdown

### Feature 1: DTOs (Data Transfer Objects)

**Purpose**: Separate API contracts from internal business logic

**ProductDto.cs**:
```csharp
public class CreateProductRequest
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
}

public class ProductResponse
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Benefits**:
- ✅ Hides internal implementation
- ✅ Allows API versioning
- ✅ Improves security
- ✅ Enables easy contract changes

---

### Feature 2: Structured Logging

**What's Logged**:
```
[12:34:56 INF] Creating product: Laptop
[12:34:56 INF] Product created with id=4
[12:34:57 INF] Fetching products with skip=0, take=10, search=all
[12:34:57 WRN] Product with id=999 not found
```

**Levels**:
- **Info**: Normal operations
- **Warning**: Potential issues (not critical)
- **Error**: Failures that need attention

---

### Feature 3: Health Checks

**Endpoint**: `GET /health`

**Response**:
```json
{
  "status": "Healthy"
}
```

**Use Cases**:
- 🏥 Kubernetes liveness probes
- 📊 Monitoring dashboards
- 🔄 Load balancer health verification
- 🚀 Deployment readiness checks

---

### Feature 4: Validation & Error Handling

**Example: Creating a Product**
```csharp
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
```

**Error Response**:
```json
{
  "success": false,
  "message": "Validation failed",
  "data": null,
  "errors": [
    "Product name is required",
    "Price must be greater than 0"
  ]
}
```

---

### Feature 5: Pagination & Filtering

**Retrieve products with filtering**:
```bash
GET /products?skip=0&take=10&search=laptop
```

**Response**:
```json
{
  "success": true,
  "message": "Products retrieved successfully",
  "data": [ /* products */ ],
  "pagination": {
    "skip": 0,
    "take": 10,
    "total": 125
  }
}
```

**Benefits**:
- 🚀 Reduced network bandwidth
- ⚡ Faster response times
- 📊 Better performance with large datasets
- 🔍 Improved user experience

---

### Feature 6: CRUD Operations

**ProductService Endpoints**:
```bash
# Create
POST /products
Body: { "name": "Laptop", "price": 999.99, "description": "..." }

# Read
GET /products
GET /products/1

# Update
PUT /products/1
Body: { "name": "Updated Name", "price": 899.99, ... }

# Delete
DELETE /products/1
```

**OrderService Endpoints**:
```bash
# Create
POST /orders
Body: { "productId": 1, "quantity": 2, "customerName": "John" }

# Read
GET /orders
GET /orders/1

# Cancel
DELETE /orders/1
```

---

### Feature 7: Inter-Service Communication

**How OrderService Calls ProductService**:

```csharp
// Step 1: Make HTTP call
var productResponse = await httpClient.GetAsync(
    $"http://product-service:8080/products/{request.ProductId}"
);

// Step 2: Check response
if (!productResponse.IsSuccessStatusCode)
    return Results.NotFound("Product not found");

// Step 3: Parse response
var responseContent = await productResponse.Content.ReadAsStringAsync();
var parsedResponse = System.Text.Json.JsonDocument.Parse(responseContent);
var product = System.Text.Json.JsonSerializer.Deserialize<Product>(
    parsedResponse.RootElement.GetProperty("data").GetRawText()
);

// Step 4: Use product data
order.ProductName = product.Name;
order.ProductPrice = product.Price;
```

**Error Handling**:
- ✅ Handles network failures gracefully
- ✅ Returns meaningful error messages
- ✅ Logs failures for debugging
- ✅ Doesn't cascade failures

---

### Feature 8: API Gateway

**Routes Configured**:
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/products",
      "UpstreamPathTemplate": "/api/products",
      "DownstreamHostAndPorts": [
        { "Host": "product-service", "Port": 8080 }
      ]
    },
    // More routes...
  ]
}
```

**Benefits**:
- 🎯 Single entry point for clients
- 🔐 Centralized authentication point
- 📋 Cross-cutting concerns (logging, CORS)
- 🛡️ Backend service protection
- ⚖️ Load balancing capabilities

---

### Feature 9: Frontend Interface

**Capabilities**:
- 📦 View all products
- ➕ Create new products
- 🛒 Place orders
- 📜 View order history
- 🔄 Real-time status updates
- 💚 Service health monitoring

**Technology**:
- Pure HTML5/CSS3/JavaScript (no frameworks)
- Responsive design
- Real-time loading states
- Error handling UI

---

## API Documentation

### Product Service

#### Get All Products
```
GET /api/products?skip=0&take=10&search=laptop
```

**Parameters**:
- `skip` (optional): Number of items to skip (default: 0)
- `take` (optional): Number of items to return (default: 10)
- `search` (optional): Search by name or description

**Response**:
```json
{
  "success": true,
  "message": "Products retrieved successfully",
  "data": [
    {
      "id": 1,
      "name": "Laptop",
      "price": 999.99,
      "description": "High-performance laptop",
      "stock": 10,
      "createdAt": "2024-01-15T10:30:00Z",
      "updatedAt": "2024-01-15T10:30:00Z"
    }
  ],
  "pagination": { "skip": 0, "take": 10, "total": 50 }
}
```

#### Get Product by ID
```
GET /api/products/1
```

**Response**:
```json
{
  "success": true,
  "message": "Product retrieved successfully",
  "data": { /* product object */ }
}
```

#### Create Product
```
POST /api/products
Content-Type: application/json

{
  "name": "Keyboard",
  "price": 79.99,
  "description": "Mechanical keyboard"
}
```

**Response**: `201 Created`

#### Update Product
```
PUT /api/products/1
Content-Type: application/json

{
  "name": "Updated Product",
  "price": 89.99,
  "description": "Updated description"
}
```

#### Delete Product
```
DELETE /api/products/1
```

### Order Service

#### Get All Orders
```
GET /api/orders?skip=0&take=10
```

#### Create Order
```
POST /api/orders
Content-Type: application/json

{
  "productId": 1,
  "quantity": 2,
  "customerName": "John Doe"
}
```

**Important**: ProductService must be running and product must exist.

#### Cancel Order
```
DELETE /api/orders/1
```

---

## Running the Project

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- Terminal/Command Prompt

### Option 1: Local Development (No Docker)

**Terminal 1 - Product Service**:
```bash
cd src/ProductService
dotnet run --urls=http://localhost:5000
```

**Terminal 2 - Order Service**:
```bash
cd src/OrderService
dotnet run --urls=http://localhost:5001
```

**Terminal 3 - API Gateway**:
```bash
cd src/ApiGateway
dotnet run --urls=http://localhost:5002
```

**Access**:
- API Gateway: http://localhost:5002
- Swagger (Product): http://localhost:5000/openapi/v1.json
- Swagger (Order): http://localhost:5001/openapi/v1.json

### Option 2: Docker Compose (Recommended)

```bash
docker-compose up --build
```

**Access**:
- Frontend: http://localhost:8080
- API Gateway: http://localhost:5002

### Stopping Services

```bash
# Stop all containers
docker-compose down

# Stop and remove volumes (clean)
docker-compose down -v
```

---

## Production Considerations

### 1. Database Integration
Currently using in-memory storage. For production:

```csharp
builder.Services.AddDbContext<ProductContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### 2. Authentication & Authorization
Add security:
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* config */ });

app.UseAuthentication();
app.UseAuthorization();
```

### 3. Rate Limiting
Prevent abuse:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

app.UseRateLimiter();
```

### 4. Caching
Improve performance:
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));
```

### 5. Circuit Breaker
Handle service failures gracefully:
```csharp
builder.Services.AddHttpClientCircuitBreaaker();
```

### 6. Distributed Tracing
Monitor requests:
```csharp
builder.Services.AddOpenTelemetry();
```

### 7. Configuration Management
Externalize configuration:
- Kubernetes ConfigMaps
- Azure Key Vault
- Environment variables

---

## Common Challenges & Solutions

### Challenge 1: Service Discovery
**Problem**: How do services find each other?

**Solutions**:
- ✅ DNS (Docker Compose uses service names)
- ✅ Kubernetes Service Discovery
- ✅ Service Registry (Consul, Eureka)
- ✅ Load Balancer

### Challenge 2: Data Consistency
**Problem**: Ensuring data consistency across services

**Solutions**:
- ✅ Distributed transactions (2-phase commit)
- ✅ Saga pattern (distributed workflow)
- ✅ Event sourcing
- ✅ Eventual consistency

### Challenge 3: Network Failures
**Problem**: What if ProductService is down?

**Solutions**:
```csharp
// Retry policy
var policy = Policy.Handle<HttpRequestException>()
    .Retry(3);

// Circuit breaker
var circuitBreaker = Policy.Handle<HttpRequestException>()
    .CircuitBreaker(3, TimeSpan.FromSeconds(5));

// Combine with Polly
var withRetryAndCircuitBreaker = Policy.Wrap(policy, circuitBreaker);
```

### Challenge 4: Debugging Distributed Systems
**Problem**: Request fails, hard to trace why

**Solutions**:
- ✅ Distributed tracing (Jaeger, Zipkin)
- ✅ Centralized logging (ELK Stack, Splunk)
- ✅ Correlation IDs
- ✅ Request/Response logging

Example Correlation ID:
```csharp
var correlationId = HttpContext.TraceIdentifier;
logger.LogInformation("Operation for {correlationId}: {operation}", correlationId, "Create order");
```

### Challenge 5: Testing
**Problem**: How to test inter-service communication?

**Solutions**:
- ✅ Unit tests (mock HttpClient)
- ✅ Integration tests (Docker Compose test environment)
- ✅ Contract tests (verify API contracts)
- ✅ E2E tests (real environment)

---

## Best Practices

### 1. **Design for Failure**
```csharp
// Always handle potential failures
try
{
    var response = await httpClient.GetAsync(url);
    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Service returned {status}", response.StatusCode);
        return Results.ServiceUnavailable();
    }
}
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Network error connecting to service");
    return Results.StatusCode(503);
}
```

### 2. **Use Asynchronous Operations**
```csharp
// Don't block threads
app.MapGet("/products", async (ILogger<Program> log) =>
{
    // Async all the way
    var products = await db.Products.ToListAsync();
    return Results.Ok(products);
});
```

### 3. **Document APIs**
```csharp
app.MapGet("/products", () => { })
    .WithName("GetProducts")
    .WithOpenApi()
    .WithDescription("Retrieve all products with pagination");
```

### 4. **Monitor Everything**
- Health checks
- Performance metrics
- Error rates
- Dependencies

### 5. **Secure Communication**
- Use HTTPS
- Validate inputs
- Implement CORS properly
- Use API keys/tokens

### 6. **Separate Concerns**
- One service = one responsibility
- Clear API boundaries
- Independent data stores
- Loose coupling

### 7. **Version APIs**
```csharp
app.MapGet("/api/v1/products", () => { })
    .WithName("GetProductsV1");

app.MapGet("/api/v2/products", () => { })
    .WithName("GetProductsV2");
```

### 8. **Use Configuration**
Don't hardcode URLs:
```csharp
var productServiceUrl = builder.Configuration["Services:ProductService:Url"];
// or use environment variables
var url = Environment.GetEnvironmentVariable("PRODUCT_SERVICE_URL");
```

---

## Troubleshooting

### Issue: "Cannot connect to ProductService"

**Cause**: Service not running or wrong URL

**Solution**:
```bash
# Check if services are running
docker-compose ps

# Check logs
docker-compose logs product-service

# Verify network connectivity (inside container)
docker exec order-service-container ping product-service
```

### Issue: "Cross-Origin Request Blocked"

**Cause**: CORS not properly configured

**Solution**: Already fixed in this project!
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
```

### Issue: "Product not found" when creating order

**Cause**: ProductService returned empty response

**Solution**:
1. Verify Product exists: `GET /api/products/1`
2. Check Product Service logs
3. Ensure correct Product ID

### Issue: Frontend shows "Error loading data"

**Cause**: API Gateway not responding

**Solution**:
```bash
# Test API directly
curl http://localhost:5002/api/products

# Check gateway logs
docker-compose logs api-gateway

# Verify routing in ocelot.json
```

### Issue: High memory usage in containers

**Cause**: Memory leaks or too many retained objects

**Solution**:
- Monitor with `docker stats`
- Add memory limits in docker-compose.yml
- Profile application with memory profilers

---

## Next Steps for Production

1. **Add Real Database**: Migrate from in-memory to SQL Server/PostgreSQL
2. **Implement Authentication**: Add JWT tokens, OAuth
3. **Add Rate Limiting**: Protect against abuse
4. **Implement Caching**: Redis for frequently accessed data
5. **Distributed Tracing**: Jaeger or Zipkin
6. **Centralized Logging**: ELK Stack or Splunk
7. **Kubernetes**: Deploy to container orchestration
8. **CI/CD Pipeline**: GitHub Actions, GitLab CI, or Azure DevOps
9. **API Versioning**: Plan for backwards compatibility
10. **Comprehensive Testing**: Unit, integration, and E2E tests

---

## Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core)
- [Microservices Architecture](https://microservices.io)
- [Docker Documentation](https://docs.docker.com)
- [Ocelot API Gateway](https://ocelot.readthedocs.io)
- [Azure Microservices Guide](https://azure.microsoft.com/en-us/solutions/microservice-applications)

---

**Happy Learning! 🚀**