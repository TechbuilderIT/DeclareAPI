# Rate Limiting & Caching

DeclareAPI provides built-in support for rate limiting and output caching to improve API performance and protect against abuse.

## Rate Limiting

### Overview

Rate limiting restricts the number of requests a client can make within a time window. DeclareAPI uses ASP.NET Core's built-in rate limiting with fixed-window algorithm.

### Configuration

```yaml
endpoints:
  create:
    method: POST
    path: /resources
    source:
      type: function
      name: sp_create_resource
    rate_limit:
      limit: 100        # Maximum requests
      window: 60        # Time window in seconds
      policy: "api"     # Optional: named policy
```

### Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `limit` | int | 100 | Maximum requests allowed in window |
| `window` | int | 60 | Time window in seconds |
| `policy` | string | auto | Named policy (auto-generated if not specified) |

### Examples

#### Basic Rate Limiting

```yaml
# 100 requests per minute
rate_limit:
  limit: 100
  window: 60
```

#### Strict Rate Limiting

```yaml
# 10 requests per minute (for expensive operations)
rate_limit:
  limit: 10
  window: 60
```

#### Named Policies

Use named policies to share limits across endpoints:

```yaml
entities:
  User:
    endpoints:
      create:
        method: POST
        path: /users
        rate_limit:
          limit: 5
          window: 60
          policy: "user_write"

      update:
        method: PUT
        path: /users/{id}
        rate_limit:
          policy: "user_write"  # Same policy = shared limit
```

### Enabling Rate Limiting

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.EnableRateLimiting = true;  // Enabled by default
});

var app = builder.Build();

app.UseDeclareApiPolicies();  // Adds rate limiting middleware
```

### Response Headers

Rate-limited responses include headers:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 42
X-RateLimit-Reset: 1699459200
```

### 429 Too Many Requests

When limit is exceeded:

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Try again in 45 seconds."
}
```

---

## Output Caching

### Overview

Output caching stores HTTP responses and serves cached content for subsequent identical requests. This dramatically improves performance for read-heavy endpoints.

### Configuration

```yaml
endpoints:
  list:
    method: GET
    path: /resources
    source:
      type: view
      name: vw_resources
    cache:
      duration: 300           # Cache for 5 minutes
      vary_by_query: true     # Different cache per query string
      vary_by_user: false     # Same cache for all users
```

### Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `duration` | int | 60 | Cache duration in seconds |
| `vary_by_query` | bool | true | Vary cache by query string |
| `vary_by_user` | bool | false | Vary cache by authenticated user |
| `vary_by_params` | list | null | Vary by specific parameters |

### Examples

#### Basic Caching

```yaml
# Cache for 5 minutes
cache:
  duration: 300
```

#### Query-Aware Caching

```yaml
# Different cache for different query parameters
cache:
  duration: 300
  vary_by_query: true
```

This means:
- `GET /products` = cache A
- `GET /products?category=books` = cache B
- `GET /products?category=electronics` = cache C

#### User-Specific Caching

```yaml
# Different cache per user
cache:
  duration: 300
  vary_by_user: true
```

Useful for personalized content:
- User A sees their cached dashboard
- User B sees their cached dashboard

#### Selective Parameter Caching

```yaml
# Only vary by specific parameters
cache:
  duration: 300
  vary_by_query: false
  vary_by_params:
    - page
    - pageSize
```

This ignores other query parameters for caching purposes.

### Enabling Caching

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.EnableCaching = true;  // Enabled by default
});

var app = builder.Build();

app.UseDeclareApiPolicies();  // Adds output cache middleware
```

### Cache Headers

Cached responses include standard HTTP cache headers:

```
Cache-Control: public, max-age=300
Age: 42
ETag: "abc123"
```

### When NOT to Cache

Avoid caching for:

- POST/PUT/PATCH/DELETE operations (mutations)
- User-specific data without `vary_by_user`
- Real-time data (stock prices, live feeds)
- Sensitive information

```yaml
# Good: Cache product catalog (public, static-ish)
list_products:
  method: GET
  path: /products
  cache:
    duration: 300

# Bad: Don't cache shopping cart (user-specific, dynamic)
get_cart:
  method: GET
  path: /cart
  # No cache here - cart changes frequently
```

---

## Combining Rate Limiting and Caching

Use both for optimal protection and performance:

```yaml
entities:
  Product:
    endpoints:
      list:
        method: GET
        path: /products
        source:
          type: view
          name: vw_products
        paginated: true
        cache:
          duration: 300
          vary_by_query: true
        rate_limit:
          limit: 1000
          window: 60

      create:
        method: POST
        path: /products
        source:
          type: function
          name: sp_create_product
        # No cache for mutations
        rate_limit:
          limit: 10
          window: 60

      search:
        method: GET
        path: /products/search
        source:
          type: function
          name: fn_search_products
        cache:
          duration: 60          # Short cache for search
          vary_by_query: true
        rate_limit:
          limit: 100            # Allow more searches
          window: 60
```

---

## Disabling Policies

### Globally

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.EnableRateLimiting = false;
    options.EnableCaching = false;
});
```

### Per Endpoint

Simply don't include the `rate_limit` or `cache` configuration for endpoints that shouldn't use them.

---

## Best Practices

### Rate Limiting

1. **Start conservative** - Begin with lower limits, increase as needed
2. **Different limits for different operations** - Reads can have higher limits than writes
3. **Use named policies** - Share limits across related endpoints
4. **Monitor and adjust** - Track 429 responses and adjust limits

### Caching

1. **Cache at the edge** - Consider CDN caching for public content
2. **Invalidate on mutation** - Clear cache when data changes
3. **Set appropriate durations** - Balance freshness vs performance
4. **Use vary-by wisely** - Too much variation defeats caching benefits

### General

1. **Test under load** - Verify behavior at scale
2. **Document limits** - Include rate limits in API documentation
3. **Handle gracefully** - Clients should handle 429 responses
4. **Consider tiers** - Different limits for different API tiers
