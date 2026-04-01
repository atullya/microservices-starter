# 🛍️ ASP.NET Core Microservices Example

A comprehensive, production-grade microservices application demonstrating industry best practices in building scalable systems.

## ✨ Features

### Core Microservices
- **Product Service**: Complete CRUD operations with pagination and filtering
- **Order Service**: Order management with inter-service communication
- **API Gateway**: Request routing, CORS handling, logging

### Production Features
- ✅ Structured Logging (Console, Debug)
- ✅ Health Checks (`/health` endpoints)
- ✅ Data Transfer Objects (DTOs)
- ✅ Comprehensive Validation
- ✅ Pagination & Filtering
- ✅ Standardized API Responses
- ✅ Error Handling
- ✅ CORS Configuration
- ✅ Async/Await Patterns
- ✅ Docker & Docker Compose

### Additional Components
- 🎨 Modern, Responsive Frontend UI
- 📊 Real-time Service Status Monitoring
- 🐳 Full Containerization

## 📋 Project Structure

```
Microservices/
├── src/
│   ├── ProductService/
│   │   ├── Models/
│   │   ├── DTOs/
│   │   ├── Program.cs
│   │   └── Dockerfile
│   ├── OrderService/
│   │   ├── Models/
│   │   ├── DTOs/
│   │   ├── Program.cs
│   │   └── Dockerfile
│   ├── ApiGateway/
│   │   ├── ocelot.json
│   │   ├── Program.cs
│   │   └── Dockerfile
│   └── Frontend/
│       └── index.html
├── docker-compose.yml
├── README.md
├── MicroservicesGuide.md
└── ADVANCED_DOCUMENTATION.md
```

## 🏗️ Architecture

```
Frontend (Port 8080)
        ↓
    API Gateway (Port 5002)
    ↙              ↘
Product Service  Order Service
(Port 5000)      (Port 5001)
                    ↓
            (calls ProductService)
```

## 🚀 Quick Start

### Option 1: Docker Compose (Recommended)

1. **Prerequisites**: Docker & Docker Compose installed

2. **Run all services**:
```bash
docker-compose up --build
```

3. **Access services**:
   - Frontend: [http://localhost:8080](http://localhost:8080)
   - API Gateway: [http://localhost:5002](http://localhost:5002)

4. **Stop services**:
```bash
docker-compose down
```

### Option 2: Local Development

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

## 📡 API Endpoints

### Product Service

```bash
# Get all products (with pagination)
GET /api/products?skip=0&take=10&search=laptop

# Get specific product
GET /api/products/1

# Create product
POST /api/products
{
  "name": "Laptop",
  "price": 999.99,
  "description": "High-performance laptop"
}

# Update product
PUT /api/products/1
{
  "name": "Updated Name",
  "price": 899.99,
  "description": "Updated description"
}

# Delete product
DELETE /api/products/1

# Health check
GET /api/products/health/check
```

### Order Service

```bash
# Get all orders (with pagination)
GET /api/orders?skip=0&take=10

# Get specific order
GET /api/orders/1

# Create order
POST /api/orders
{
  "productId": 1,
  "quantity": 2,
  "customerName": "John Doe"
}

# Cancel order
DELETE /api/orders/1

# Health check
GET /api/orders/health/check
```

## 💻 Using the Frontend

1. Open [http://localhost:8080](http://localhost:8080) in your browser
2. **Create Products**: Enter product details and click "✨ Create Product"
3. **View Products**: See all products in real-time
4. **Place Orders**: Select product ID and quantity
5. **Monitor Status**: Check service health indicators

## 📚 Documentation

- **[README.md](README.md)** - Quick start guide
- **[MicroservicesGuide.md](MicroservicesGuide.md)** - Beginner-friendly microservices concepts
- **[ADVANCED_DOCUMENTATION.md](ADVANCED_DOCUMENTATION.md)** - In-depth feature breakdown and production considerations

## 🔑 Key Features Explained

### 1. Pagination
```
GET /api/products?skip=0&take=10
```
- Reduces bandwidth
- Improves performance
- Essential for large datasets

### 2. Filtering
```
GET /api/products?search=laptop
```
- Search products by name or description
- Quick data retrieval

### 3. Health Checks
```
GET /health
```
- Used by load balancers
- Kubernetes liveness probes
- Service readiness verification

### 4. Logging
- Every operation is logged
- Helps with debugging
- Check console for detailed info

### 5. Error Handling
- Validation on all inputs
- Meaningful error messages
- Proper HTTP status codes

### 6. Inter-Service Communication
-OrderService calls ProductService via HTTP
- Handles failures gracefully
- Async patterns throughout

## 🐳 Docker Usage

### Build Images
```bash
docker-compose build
```

### View Running Containers
```bash
docker-compose ps
```

### View Logs
```bash
# All services
docker-compose logs

# Specific service
docker-compose logs product-service

# Follow logs
docker-compose logs -f api-gateway
```

### Execute Command in Container
```bash
docker exec product-service dotnet --version
```

## 📊 Testing the APIs

### Using curl

```bash
# Get products
curl http://localhost:5002/api/products

# Create product
curl -X POST http://localhost:5002/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Mouse","price":29.99,"description":"Wireless mouse"}'

# Create order
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{"productId":1,"quantity":1,"customerName":"Alice"}'
```

### Using Postman
1. Import the API Gateway URL: `http://localhost:5002`
2. See OpenAPI documentation for endpoints

### Using Browser
- Open [http://localhost:8080](http://localhost:8080)
- Use the interactive frontend

## 🏭 Production Deployment

For production, consider:

1. **Database**: Replace in-memory storage with SQL Server/PostgreSQL
2. **Authentication**: Add JWT or OAuth
3. **Rate Limiting**: Prevent abuse
4. **Caching**: Redis for performance
5. **Kubernetes**: Container orchestration
6. **Monitoring**: Prometheus & Grafana
7. **Logging**: ELK Stack
8. **Tracing**: Jaeger or Zipkin

See [ADVANCED_DOCUMENTATION.md](ADVANCED_DOCUMENTATION.md) for details.

## ⚠️ Common Issues

### Services can't find each other
- Ensure all containers are running: `docker-compose ps`
- Check Docker network: `docker network ls`
- View logs: `docker-compose logs`

### Port already in use
- Change ports in `docker-compose.yml`
- Or kill process using the port

### Frontend can't connect to API
- Verify API Gateway is running
- Check browser console for CORS errors
- Ensure URLs match your setup

## 🎓 Learning Path

1. Start with [README.md](README.md) - Overview
2. Read [MicroservicesGuide.md](MicroservicesGuide.md) - Concepts
3. Study [ADVANCED_DOCUMENTATION.md](ADVANCED_DOCUMENTATION.md) - Details
4. Explore the code:
   - [ProductService](src/ProductService/Program.cs)
   - [OrderService](src/OrderService/Program.cs)
   - [ApiGateway](src/ApiGateway/Program.cs)
   - [Frontend](src/Frontend/index.html)

## 📦 Dependencies

- .NET 10 SDK
- Docker & Docker Compose
- Modern web browser

No additional NuGet packages required for core functionality!

## 🤝 Contributing

Feel free to extend this project:
- Add database integration
- Implement authentication
- Add more services
- Enhance the frontend
- Add comprehensive tests

## 📝 License

MIT License - Free to use and modify

## 🚀 Next Steps

1. Explore the code
2. Try creating products and orders
3. Read ADVANCED_DOCUMENTATION.md for production patterns
4. Experiment with adding new features
5. Deploy to cloud (Azure, AWS, GCP)

---

**Happy learning! 🎉**

For detailed information, see [ADVANCED_DOCUMENTATION.md](ADVANCED_DOCUMENTATION.md)