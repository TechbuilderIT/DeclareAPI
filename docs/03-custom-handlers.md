# Custom Handlers

Custom handlers provide an escape hatch from declarative YAML to imperative C# code. Use them when you need:

- Complex business logic
- External service integration
- Custom validation rules
- Multi-step operations
- Anything beyond simple CRUD

## Overview

DeclareAPI follows the principle: **"80% declarative, 20% imperative"**. Custom handlers handle the 20% where C# is better suited.

## Creating a Custom Handler

### Step 1: Define Request/Response Models

```csharp
// Request model - properties map to request body
public record CreateOrderRequest(
    Guid CustomerId,
    List<OrderItemRequest> Items,
    string? CouponCode,
    ShippingAddress ShippingAddress
);

public record OrderItemRequest(
    Guid ProductId,
    int Quantity
);

public record ShippingAddress(
    string Street,
    string City,
    string State,
    string ZipCode,
    string Country
);

// Response model - returned to client
public record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingCost,
    decimal Total,
    string Status,
    DateTime EstimatedDelivery
);
```

### Step 2: Implement the Handler

```csharp
using Techbuilder.DeclareAPI.Core.Abstractions;

[HandlerName("CreateOrder")]  // Name referenced in YAML
public class CreateOrderHandler : ICustomHandler<CreateOrderRequest, CreateOrderResponse>
{
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly ICouponService _couponService;
    private readonly IShippingService _shippingService;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IOrderService orderService,
        IInventoryService inventoryService,
        ICouponService couponService,
        IShippingService shippingService,
        ILogger<CreateOrderHandler> logger)
    {
        _orderService = orderService;
        _inventoryService = inventoryService;
        _couponService = couponService;
        _shippingService = shippingService;
        _logger = logger;
    }

    public async Task<CreateOrderResponse> HandleAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating order for customer {CustomerId} with {ItemCount} items",
            request.CustomerId,
            request.Items.Count);

        // 1. Validate inventory
        foreach (var item in request.Items)
        {
            var available = await _inventoryService.CheckAvailabilityAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);

            if (!available)
            {
                throw new InvalidOperationException(
                    $"Product {item.ProductId} is not available in requested quantity");
            }
        }

        // 2. Calculate pricing
        var subtotal = await _orderService.CalculateSubtotalAsync(
            request.Items,
            cancellationToken);

        // 3. Apply coupon if provided
        decimal discount = 0;
        if (!string.IsNullOrEmpty(request.CouponCode))
        {
            discount = await _couponService.ApplyCouponAsync(
                request.CouponCode,
                subtotal,
                cancellationToken);
        }

        // 4. Calculate shipping
        var shippingCost = await _shippingService.CalculateShippingAsync(
            request.ShippingAddress,
            request.Items,
            cancellationToken);

        // 5. Create the order
        var order = await _orderService.CreateOrderAsync(
            request.CustomerId,
            request.Items,
            request.ShippingAddress,
            subtotal,
            discount,
            shippingCost,
            cancellationToken);

        // 6. Reserve inventory
        await _inventoryService.ReserveInventoryAsync(
            order.Id,
            request.Items,
            cancellationToken);

        _logger.LogInformation(
            "Order {OrderNumber} created successfully with total {Total}",
            order.OrderNumber,
            order.Total);

        return new CreateOrderResponse(
            order.Id,
            order.OrderNumber,
            subtotal,
            discount,
            shippingCost,
            order.Total,
            order.Status,
            order.EstimatedDelivery);
    }
}
```

### Step 3: Reference in YAML

```yaml
entities:
  Order:
    endpoints:
      create:
        method: POST
        path: /orders
        handler: CreateOrder  # Matches [HandlerName("CreateOrder")]
        authorize: true
```

### Step 4: Register Handler Assembly

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess(sp => new DapperDataAccess(connectionString, DatabaseProvider.PostgreSQL));

    // Scan assembly for handlers
    options.ScanHandlersFrom<Program>();
    // Or specify type from another assembly
    options.ScanHandlersFrom<CreateOrderHandler>();
    // Or specify assembly directly
    options.ScanHandlersFrom(typeof(CreateOrderHandler).Assembly);
});
```

## Handler Naming

### Using HandlerNameAttribute

```csharp
[HandlerName("MyCustomName")]
public class SomeHandler : ICustomHandler<Request, Response> { }
```

Reference in YAML:
```yaml
handler: MyCustomName
```

### Default Naming Convention

If no `[HandlerName]` attribute is present, the handler name is derived from the class name by removing the "Handler" suffix:

| Class Name | Handler Name |
|------------|--------------|
| `CreateOrderHandler` | `CreateOrder` |
| `ProcessPaymentHandler` | `ProcessPayment` |
| `SendNotificationHandler` | `SendNotification` |

## Dependency Injection

Handlers are registered with scoped lifetime and support full dependency injection:

```csharp
public class MyHandler : ICustomHandler<Request, Response>
{
    private readonly IDbContext _dbContext;          // Scoped
    private readonly ILogger<MyHandler> _logger;     // Singleton
    private readonly IHttpClientFactory _httpFactory; // Singleton
    private readonly ICurrentUser _currentUser;      // Scoped

    public MyHandler(
        IDbContext dbContext,
        ILogger<MyHandler> logger,
        IHttpClientFactory httpFactory,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpFactory = httpFactory;
        _currentUser = currentUser;
    }

    public async Task<Response> HandleAsync(Request request, CancellationToken ct)
    {
        // All dependencies are available
    }
}
```

## Request Binding

The request model is automatically bound from the HTTP request body (for POST/PUT/PATCH) using JSON deserialization.

### Route Parameters

Route parameters from the URL path are not automatically bound to the request model. If you need route parameters, access them through the handler or use a different approach:

```yaml
endpoints:
  update:
    method: PUT
    path: /orders/{id}
    handler: UpdateOrder
    params:
      - { name: id, type: uuid, from: route }
```

For now, route parameters are handled separately. Consider including them in your request model and extracting from the route manually if needed.

## Error Handling

Handlers can throw exceptions which are converted to HTTP responses:

```csharp
public async Task<Response> HandleAsync(Request request, CancellationToken ct)
{
    var entity = await _repository.GetByIdAsync(request.Id, ct);

    if (entity == null)
    {
        throw new KeyNotFoundException($"Entity {request.Id} not found");
        // Returns 404 Not Found
    }

    if (!entity.CanBeModified)
    {
        throw new InvalidOperationException("Entity cannot be modified");
        // Returns 400 Bad Request
    }

    // ... process
}
```

### Exception to HTTP Status Mapping

| Exception Type | HTTP Status |
|---------------|-------------|
| `KeyNotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `InvalidOperationException` | 400 Bad Request |
| `ArgumentException` | 400 Bad Request |
| Other exceptions | 500 Internal Server Error |

## Combining with Authorization

```yaml
endpoints:
  admin_action:
    method: POST
    path: /admin/dangerous-operation
    handler: DangerousOperationHandler
    authorize: true          # Requires authentication
    policy: admin_only       # Requires admin policy
    roles:
      - superadmin
```

## Best Practices

### 1. Keep Handlers Focused

Each handler should do one thing well:

```csharp
// Good: Focused handler
public class CreateOrderHandler : ICustomHandler<CreateOrderRequest, CreateOrderResponse>

// Bad: Handler doing too much
public class OrderHandler : ICustomHandler<OrderRequest, OrderResponse>
// What does it do? Create? Update? Delete?
```

### 2. Use Meaningful Names

```csharp
// Good
[HandlerName("ProcessRefund")]
[HandlerName("SendWelcomeEmail")]
[HandlerName("GenerateInvoicePdf")]

// Bad
[HandlerName("Handler1")]
[HandlerName("DoStuff")]
```

### 3. Validate Early

```csharp
public async Task<Response> HandleAsync(Request request, CancellationToken ct)
{
    // Validate at the start
    if (request.Amount <= 0)
        throw new ArgumentException("Amount must be positive");

    if (request.Items.Count == 0)
        throw new ArgumentException("At least one item required");

    // Then process
}
```

### 4. Log Important Operations

```csharp
public async Task<Response> HandleAsync(Request request, CancellationToken ct)
{
    _logger.LogInformation("Starting operation for {EntityId}", request.EntityId);

    try
    {
        var result = await ProcessAsync(request, ct);
        _logger.LogInformation("Operation completed successfully");
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Operation failed for {EntityId}", request.EntityId);
        throw;
    }
}
```

## Testing Handlers

```csharp
public class CreateOrderHandlerTests
{
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<IInventoryService> _inventoryServiceMock;
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _orderServiceMock = new Mock<IOrderService>();
        _inventoryServiceMock = new Mock<IInventoryService>();

        _handler = new CreateOrderHandler(
            _orderServiceMock.Object,
            _inventoryServiceMock.Object,
            Mock.Of<ICouponService>(),
            Mock.Of<IShippingService>(),
            Mock.Of<ILogger<CreateOrderHandler>>());
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_CreatesOrder()
    {
        // Arrange
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new List<OrderItemRequest> { new(Guid.NewGuid(), 2) },
            null,
            new ShippingAddress("123 Main St", "City", "ST", "12345", "US"));

        _inventoryServiceMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(It.IsAny<...>()))
            .ReturnsAsync(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-001" });

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ORD-001", result.OrderNumber);
    }
}
```
