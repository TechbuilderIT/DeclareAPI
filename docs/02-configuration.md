# Configuration Reference

Complete reference for DeclareAPI YAML configuration.

## Schema Overview

```yaml
version: "1.0"                    # Required: config version

database:                          # Required: database settings
  provider: postgresql | sqlserver | mysql | sqlite
  connection: "connection_string"

settings:                          # Optional: global settings
  base_path: /api
  default_page_size: 25
  max_page_size: 100
  generate_openapi: true

entities:                          # Required: entity definitions
  EntityName:
    description: "Entity description"
    endpoints:
      endpoint_name:
        # ... endpoint configuration
```

## Database Configuration

```yaml
database:
  provider: postgresql    # Database provider
  connection: "${DB_CONNECTION}"  # Connection string (supports env vars)
```

### Supported Providers

| Provider | Value | Notes |
|----------|-------|-------|
| PostgreSQL | `postgresql` | Primary supported provider |
| SQL Server | `sqlserver` | Requires custom IDataAccess |
| MySQL | `mysql` | Requires custom IDataAccess |
| SQLite | `sqlite` | Requires custom IDataAccess |

## Global Settings

```yaml
settings:
  base_path: /api           # Prefix for all endpoints
  default_page_size: 25     # Default pagination size
  max_page_size: 100        # Maximum allowed page size
  generate_openapi: true    # Enable OpenAPI schema generation
```

## Entity Configuration

```yaml
entities:
  Product:
    description: "Product catalog"
    endpoints:
      # ... endpoints
```

## Endpoint Configuration

### Basic Structure

```yaml
endpoints:
  endpoint_name:
    method: GET | POST | PUT | PATCH | DELETE
    path: /resource/{param}
    source:
      type: view | table | function | procedure
      name: database_object_name
```

### HTTP Methods

| Method | Use Case |
|--------|----------|
| `GET` | Retrieve data (list or single) |
| `POST` | Create new resource |
| `PUT` | Full update of resource |
| `PATCH` | Partial update of resource |
| `DELETE` | Remove resource |

### Data Sources

#### View
```yaml
source:
  type: view
  name: vw_products_active
```

#### Table
```yaml
source:
  type: table
  name: products
```

#### Function (returns data)
```yaml
source:
  type: function
  name: fn_get_product_details
```

#### Procedure (executes action)
```yaml
source:
  type: procedure
  name: sp_create_product
```

### Parameters

Route and query parameters:

```yaml
params:
  - name: id
    type: uuid
    from: route      # From URL path

  - name: status
    type: string
    from: query      # From query string
```

#### Parameter Types

| Type | Description | Example |
|------|-------------|---------|
| `uuid` | UUID/GUID | `550e8400-e29b-41d4-a716-446655440000` |
| `int` | Integer | `42` |
| `string` | Text | `"hello"` |
| `decimal` | Decimal number | `19.99` |
| `date` | Date only | `2024-01-15` |
| `datetime` | Date and time | `2024-01-15T10:30:00Z` |
| `bool` | Boolean | `true` / `false` |

### Request Body Fields

For POST, PUT, PATCH endpoints:

```yaml
fields:
  - name: title
    type: string
    required: true
    min: 1           # Min length for strings
    max: 200         # Max length for strings

  - name: price
    type: decimal
    required: true
    min: 0           # Min value for numbers
    max: 99999.99    # Max value for numbers

  - name: email
    type: string
    required: true
    pattern: "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"

  - name: quantity
    type: int
    required: false  # Optional field
```

### Filters

For GET endpoints with filtering:

```yaml
filters:
  - field: name
    operator: contains

  - field: category
    operator: equals

  - field: price
    operator: between

  - field: status
    operator: in
```

#### Filter Operators

| Operator | SQL Equivalent | Example Query |
|----------|----------------|---------------|
| `equals` | `= value` | `?status=active` |
| `contains` | `LIKE '%value%'` | `?name=john` |
| `starts_with` | `LIKE 'value%'` | `?code=PRD` |
| `ends_with` | `LIKE '%value'` | `?email=@gmail.com` |
| `gt` | `> value` | `?price_gt=100` |
| `gte` | `>= value` | `?price_gte=100` |
| `lt` | `< value` | `?price_lt=50` |
| `lte` | `<= value` | `?price_lte=50` |
| `between` | `BETWEEN a AND b` | `?price_min=10&price_max=100` |
| `in` | `IN (a, b, c)` | `?status=active,pending` |

### Sorting

```yaml
sort: [name, price, created_at]  # Allowed sort columns
```

Usage:
```
GET /products?sort=price&order=desc
GET /products?sort=name&order=asc
```

### Pagination

```yaml
paginated: true
```

Usage:
```
GET /products?page=1&pageSize=10
```

Response includes:
```json
{
  "data": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": 150,
  "totalPages": 15
}
```

### Return Type

For functions that return a single value:

```yaml
returns: uuid | int | string | object
```

## Complete Example

```yaml
version: "1.0"

database:
  provider: postgresql
  connection: "${DB_CONNECTION}"

settings:
  base_path: /api
  default_page_size: 25
  max_page_size: 100

entities:
  Product:
    description: "Product catalog management"

    endpoints:
      list:
        method: GET
        path: /products
        source:
          type: view
          name: vw_products_active
        filters:
          - { field: name, operator: contains }
          - { field: category, operator: equals }
          - { field: price, operator: between }
          - { field: status, operator: in }
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
          name: vw_product_detail
        params:
          - { name: id, type: uuid, from: route }
        cache:
          duration: 600

      create:
        method: POST
        path: /products
        source:
          type: function
          name: sp_create_product
        fields:
          - { name: name, type: string, required: true, max: 200 }
          - { name: description, type: string, max: 2000 }
          - { name: price, type: decimal, required: true, min: 0 }
          - { name: category, type: string, required: true }
          - { name: sku, type: string, required: true, pattern: "^[A-Z]{3}-\\d{4}$" }
        returns: uuid
        authorize: true
        rate_limit:
          limit: 10
          window: 60

      update:
        method: PUT
        path: /products/{id}
        source:
          type: function
          name: sp_update_product
        params:
          - { name: id, type: uuid, from: route }
        fields:
          - { name: name, type: string, max: 200 }
          - { name: description, type: string, max: 2000 }
          - { name: price, type: decimal, min: 0 }
          - { name: category, type: string }
        authorize: true

      delete:
        method: DELETE
        path: /products/{id}
        source:
          type: function
          name: sp_delete_product
        params:
          - { name: id, type: uuid, from: route }
        policy: admin_only
        roles:
          - admin
```
