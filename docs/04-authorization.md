# Authorization

DeclareAPI integrates with ASP.NET Core's authorization system to secure your endpoints.

## Overview

Three authorization options are available:

| Option | Use Case |
|--------|----------|
| `authorize: true` | Require any authenticated user |
| `policy: "name"` | Require specific authorization policy |
| `roles: [list]` | Require specific roles |

## Basic Authentication

Require any authenticated user:

```yaml
endpoints:
  protected:
    method: GET
    path: /protected-resource
    source:
      type: view
      name: vw_protected_data
    authorize: true
```

## Policy-Based Authorization

Use named authorization policies:

```yaml
endpoints:
  admin_only:
    method: DELETE
    path: /resources/{id}
    source:
      type: function
      name: sp_delete_resource
    params:
      - { name: id, type: uuid, from: route }
    policy: admin_only
```

### Defining Policies in C#

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin_only", policy =>
        policy.RequireRole("admin"));

    options.AddPolicy("premium_user", policy =>
        policy.RequireClaim("subscription", "premium", "enterprise"));

    options.AddPolicy("age_verified", policy =>
        policy.RequireAssertion(context =>
        {
            var birthDate = context.User.FindFirst("birthdate")?.Value;
            if (DateTime.TryParse(birthDate, out var date))
            {
                return DateTime.Today.Year - date.Year >= 18;
            }
            return false;
        }));

    options.AddPolicy("department_manager", policy =>
        policy.RequireClaim("role", "manager")
              .RequireClaim("department"));
});
```

## Role-Based Authorization

Require specific roles:

```yaml
endpoints:
  manage_users:
    method: PUT
    path: /users/{id}
    source:
      type: function
      name: sp_update_user
    params:
      - { name: id, type: uuid, from: route }
    roles:
      - admin
      - hr_manager
```

Users must have at least one of the specified roles.

## Combining Authorization Options

You can combine multiple authorization options:

```yaml
endpoints:
  sensitive_operation:
    method: POST
    path: /sensitive
    handler: SensitiveOperationHandler
    authorize: true       # Must be authenticated
    policy: admin_only    # Must satisfy admin_only policy
    roles:                # Must have one of these roles
      - superadmin
      - security_admin
```

When combined, ALL conditions must be satisfied.

## Configuring Authentication

### JWT Bearer Authentication

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://your-auth-server.com";
        options.Audience = "your-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthorization();
```

### Cookie Authentication

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization();
```

### OAuth/OpenID Connect

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = "https://your-identity-provider.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.ResponseType = "code";
    options.SaveTokens = true;
});

builder.Services.AddAuthorization();
```

## Middleware Order

Authentication and authorization middleware must be added in the correct order:

```csharp
var app = builder.Build();

// 1. Observability (early in pipeline)
app.UseDeclareApiObservability();

// 2. Authentication (must come before authorization)
app.UseAuthentication();

// 3. Authorization
app.UseAuthorization();

// 4. Rate limiting and caching
app.UseDeclareApiPolicies();

// 5. Map endpoints
app.MapDeclareApi();
app.MapDeclareApiHealthChecks();
```

## Public vs Protected Endpoints

Mix public and protected endpoints in the same entity:

```yaml
entities:
  Product:
    endpoints:
      # Public - anyone can view products
      list:
        method: GET
        path: /products
        source:
          type: view
          name: vw_products
        paginated: true
        # No authorize, policy, or roles = public

      # Protected - only authenticated users can create
      create:
        method: POST
        path: /products
        source:
          type: function
          name: sp_create_product
        fields:
          - { name: name, type: string, required: true }
          - { name: price, type: decimal, required: true }
        authorize: true

      # Admin only - requires admin role
      delete:
        method: DELETE
        path: /products/{id}
        source:
          type: function
          name: sp_delete_product
        params:
          - { name: id, type: uuid, from: route }
        roles:
          - admin
```

## Error Responses

### 401 Unauthorized

Returned when authentication is required but not provided:

```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### 403 Forbidden

Returned when authenticated but not authorized:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

## Custom Authorization Handlers

For complex authorization logic, create custom handlers:

```csharp
public class ResourceOwnerRequirement : IAuthorizationRequirement { }

public class ResourceOwnerHandler : AuthorizationHandler<ResourceOwnerRequirement>
{
    private readonly IResourceService _resourceService;

    public ResourceOwnerHandler(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return;
        }

        // Get resource ID from route
        if (context.Resource is HttpContext httpContext)
        {
            var resourceId = httpContext.Request.RouteValues["id"]?.ToString();
            if (resourceId != null)
            {
                var isOwner = await _resourceService.IsOwnerAsync(
                    Guid.Parse(resourceId),
                    Guid.Parse(userId));

                if (isOwner)
                {
                    context.Succeed(requirement);
                }
            }
        }
    }
}

// Register
builder.Services.AddScoped<IAuthorizationHandler, ResourceOwnerHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("resource_owner", policy =>
        policy.Requirements.Add(new ResourceOwnerRequirement()));
});
```

Use in YAML:
```yaml
endpoints:
  update_own_resource:
    method: PUT
    path: /my-resources/{id}
    handler: UpdateResourceHandler
    policy: resource_owner
```

## Best Practices

1. **Use policies over roles** - Policies are more flexible and testable
2. **Keep policies granular** - One policy per permission
3. **Document your policies** - Maintain a policy registry
4. **Test authorization** - Write tests for authorization rules
5. **Use claims appropriately** - Don't overload JWT with unnecessary data
