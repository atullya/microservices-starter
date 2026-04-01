# ASP.NET Core Microservices Example

This is a simple microservices example in ASP.NET Core demonstrating:

- Product Service: Manages product catalog
- Order Service: Handles order processing with inter-service communication
- API Gateway: Routes requests using Ocelot

## Architecture

```
Client -> API Gateway (Port 5002) -> Product Service (Port 5000)
                                   -> Order Service (Port 5001)
```

Order Service calls Product Service to get product details.

## Running Locally

### Prerequisites
- .NET 10 SDK
- Docker (optional, for containerized run)

### Without Docker
```bash
# Terminal 1: Product Service
cd src/ProductService
dotnet run --urls=http://localhost:5000

# Terminal 2: Order Service
cd src/OrderService
dotnet run --urls=http://localhost:5001

# Terminal 3: API Gateway
cd src/ApiGateway
dotnet run --urls=http://localhost:5002
```

### With Docker
```bash
docker-compose up --build
```

## API Endpoints

### Via API Gateway (Port 5002)
- `GET /api/products` - Get all products
- `GET /api/products/{id}` - Get product by ID
- `POST /api/products` - Create product
- `GET /api/orders` - Get all orders
- `POST /api/orders` - Create order

### Direct Service Access
- Product Service: `http://localhost:5000/products`
- Order Service: `http://localhost:5001/orders`

## Example Requests

### Create Product
```bash
curl -X POST http://localhost:5002/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":49.99,"description":"Mechanical keyboard"}'
```

### Create Order
```bash
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{"productId":1,"quantity":2}'
```

## Learning Resources

See [MicroservicesGuide.md](MicroservicesGuide.md) for detailed explanations of microservices concepts, pain points, and best practices.