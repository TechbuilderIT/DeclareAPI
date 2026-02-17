# DeclareAPI

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Tests](https://img.shields.io/badge/Tests-85%20passing-brightgreen)]()

**Build REST APIs from YAML configuration with seamless C# escape hatches.**

DeclareAPI is a .NET 8 library that enables creating fully-functional REST APIs from declarative YAML configuration. Start with 40 lines of YAML, scale with C# — without rewriting anything.

[Leia em Portugues](README.pt-BR.md)

## Features

- **Declarative Configuration**: Define endpoints, filters, validation, and more in YAML
- **Database-First**: Direct mapping to views, stored procedures, and functions
- **Automatic Validation**: FluentValidation rules generated from config
- **OpenAPI/Swagger**: Auto-generated documentation
- **Custom Handlers**: Escape to C# when you need imperative logic
- **Authorization**: Built-in support for policies and roles
- **Rate Limiting**: Fixed-window rate limiting per endpoint
- **Output Caching**: Configurable caching with vary-by options
- **Observability**: Correlation IDs, structured logging, health checks

## Quick Start

### 1. Install the Package

```bash
dotnet add package Techbuilder.DeclareAPI
dotnet add package Techbuilder.DeclareAPI.Dapper  # For PostgreSQL/Dapper
```

### 2. Create Configuration File

Create `declareapi.yaml` in your project root:

```yaml
version: "1.0"

database:
  provider: postgresql
  connection: "${DB_CONNECTION}"

settings:
  base_path: /api
  default_page_size: 25

entities:
  Product:
    endpoints:
      list:
        method: GET
        path: /products
        source:
          type: view
          name: vw_products
        filters:
          - { field: name, operator: contains }
          - { field: category, operator: equals }
        sort: [name, price, created_at]
        paginated: true
        cache:
          duration: 300
          vary_by_query: true

      get:
        method: GET
        path: /products/{id}
        source:
          type: view
          name: vw_products
        params:
          - { name: id, type: uuid, from: route }

      create:
        method: POST
        path: /products
        source:
          type: function
          name: sp_create_product
        fields:
          - { name: name, type: string, required: true, max: 200 }
          - { name: price, type: decimal, required: true, min: 0 }
          - { name: category, type: string, required: true }
        returns: uuid
        authorize: true
        rate_limit:
          limit: 10
          window: 60
```

### 3. Configure Your Application

```csharp
using Techbuilder.DeclareAPI.Dapper;
using Techbuilder.DeclareAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess(sp => new DapperDataAccess(connectionString, DatabaseProvider.PostgreSQL));
    options.ScanHandlersFrom<Program>();  // Optional: scan for custom handlers

    // All enabled by default
    options.EnableRequestLogging = true;
    options.EnableCorrelationId = true;
    options.EnableHealthChecks = true;
    options.EnableRateLimiting = true;
    options.EnableCaching = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Add observability middleware (correlation ID, logging)
app.UseDeclareApiObservability();

// Add authentication/authorization if needed
// app.UseAuthentication();
// app.UseAuthorization();

// Add rate limiting and caching
app.UseDeclareApiPolicies();

// Map all endpoints from YAML
app.MapDeclareApi();
app.MapDeclareApiHealthChecks();

app.Run();
```

### 4. Run Your API

```bash
dotnet run
```

Your API is now available at `http://localhost:5000/api/products` with:
- Pagination (`?page=1&pageSize=10`)
- Filtering (`?name=widget&category=electronics`)
- Sorting (`?sort=price&order=desc`)
- OpenAPI documentation at `/swagger`

## Configuration Reference

### Endpoint Configuration

```yaml
endpoints:
  endpoint_name:
    method: GET | POST | PUT | PATCH | DELETE
    path: /resource/{id}
    source:
      type: view | table | function | procedure
      name: database_object_name

    # Route/Query Parameters
    params:
      - name: id
        type: uuid | int | string | date | datetime | decimal | bool
        from: route | query

    # Request Body Fields (POST/PUT/PATCH)
    fields:
      - name: field_name
        type: string | int | decimal | date | datetime | bool | uuid
        required: true | false
        min: 0           # For numbers: minimum value; for strings: min length
        max: 100         # For numbers: maximum value; for strings: max length
        pattern: "regex" # Regex validation for strings

    # Query Filters (GET endpoints)
    filters:
      - field: column_name
        operator: equals | contains | starts_with | ends_with | gt | gte | lt | lte | between | in

    # Sorting
    sort: [column1, column2]  # Allowed sort columns

    # Pagination
    paginated: true | false

    # Return type for functions
    returns: uuid | int | string | object

    # Authorization
    authorize: true           # Requires authentication
    policy: "policy_name"     # Named authorization policy
    roles:                    # Role-based authorization
      - admin
      - manager

    # Rate Limiting
    rate_limit:
      limit: 100              # Requests allowed
      window: 60              # Time window in seconds
      policy: "custom_name"   # Optional: named policy

    # Caching
    cache:
      duration: 300           # Cache duration in seconds
      vary_by_query: true     # Vary cache by query string
      vary_by_user: false     # Vary cache by authenticated user
      vary_by_params:         # Vary by specific parameters
        - page
        - pageSize

    # Custom Handler (escape to C#)
    handler: MyCustomHandler
```

## Custom Handlers

When you need imperative logic, create a custom handler:

```csharp
using Techbuilder.DeclareAPI.Core.Abstractions;

// Request model
public record CreateOrderRequest(
    Guid CustomerId,
    List<OrderItem> Items,
    string? Notes
);

// Response model
public record CreateOrderResponse(
    Guid OrderId,
    decimal Total,
    string Status
);

// Handler implementation
[HandlerName("CreateOrder")]  // Name used in YAML config
public class CreateOrderHandler : ICustomHandler<CreateOrderRequest, CreateOrderResponse>
{
    private readonly IOrderService _orderService;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(IOrderService orderService, ILogger<CreateOrderHandler> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<CreateOrderResponse> HandleAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

        var order = await _orderService.CreateOrderAsync(
            request.CustomerId,
            request.Items,
            request.Notes,
            cancellationToken);

        return new CreateOrderResponse(order.Id, order.Total, order.Status);
    }
}
```

Reference in YAML:

```yaml
endpoints:
  create_order:
    method: POST
    path: /orders
    handler: CreateOrder  # Matches [HandlerName("CreateOrder")]
    authorize: true
```

## Database Support

### PostgreSQL with Dapper

```csharp
options.UseDataAccess(sp => new DapperDataAccess(
    connectionString,
    DatabaseProvider.PostgreSQL
));
```

### Custom Data Access

Implement `IDataAccess` for other databases:

```csharp
public interface IDataAccess
{
    Task<IEnumerable<dynamic>> QueryAsync(string sql, object? parameters = null);
    Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null);
    Task<int> ExecuteAsync(string sql, object? parameters = null);
}
```

## Observability

### Health Checks

```csharp
app.MapDeclareApiHealthChecks();
// GET /health       - Liveness probe
// GET /health/ready - Readiness probe (includes DB check)
```

### Correlation IDs

All requests are tagged with `X-Correlation-ID` header for distributed tracing.

### Structured Logging

Request/response logging with timing, status codes, and correlation IDs.

## Project Structure

```
Techbuilder.DeclareAPI/
├── Techbuilder.DeclareAPI.Core/       # Interfaces, models, configuration
│   ├── Abstractions/                   # IDataAccess, ICustomHandler
│   ├── Configuration/                  # Config models, ConfigLoader
│   ├── Validation/                     # DynamicValidator
│   └── Query/                          # FilterQueryBuilder
├── Techbuilder.DeclareAPI/            # Main library
│   ├── Routing/                        # RouteGenerator
│   ├── Handlers/                       # HandlerRegistry, HandlerInvoker
│   ├── RateLimiting/                   # Rate limiting extensions
│   ├── Caching/                        # Caching extensions
│   ├── Observability/                  # Middleware, health checks
│   └── Extensions/                     # Service registration
├── Techbuilder.DeclareAPI.Dapper/     # Dapper implementation
└── Techbuilder.DeclareAPI.Tests/      # Unit tests (85 tests)
```

## Running the Sample

```bash
cd Techbuilder.DeclareAPI.Sample

# Start PostgreSQL
docker-compose up -d

# Run the API
dotnet run

# Open Swagger UI
open http://localhost:5000/swagger
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [.NET 8](https://dotnet.microsoft.com/)
- YAML parsing by [YamlDotNet](https://github.com/aaubry/YamlDotNet)
- Data access with [Dapper](https://github.com/DapperLib/Dapper)
- Validation with [FluentValidation](https://fluentvalidation.net/)
