# Getting Started with DeclareAPI

This guide will help you set up DeclareAPI in your .NET 8 project.

## Prerequisites

- .NET 8 SDK
- PostgreSQL (or implement your own `IDataAccess`)
- Your favorite IDE (Visual Studio, VS Code, Rider)

## Installation

### Using .NET CLI

```bash
dotnet new webapi -n MyApi
cd MyApi
dotnet add package Techbuilder.DeclareAPI
dotnet add package Techbuilder.DeclareAPI.Dapper
```

### Using Package Manager

```powershell
Install-Package Techbuilder.DeclareAPI
Install-Package Techbuilder.DeclareAPI.Dapper
```

## Basic Setup

### Step 1: Create the Configuration File

Create a file named `declareapi.yaml` in your project root:

```yaml
version: "1.0"

database:
  provider: postgresql
  connection: "${DB_CONNECTION}"

settings:
  base_path: /api
  default_page_size: 25
  max_page_size: 100
  generate_openapi: true

entities:
  User:
    description: "User management"
    endpoints:
      list:
        method: GET
        path: /users
        source:
          type: table
          name: users
        filters:
          - { field: email, operator: contains }
          - { field: active, operator: equals }
        sort: [name, created_at]
        paginated: true

      get:
        method: GET
        path: /users/{id}
        source:
          type: table
          name: users
        params:
          - { name: id, type: uuid, from: route }
```

### Step 2: Configure Program.cs

```csharp
using Techbuilder.DeclareAPI.Dapper;
using Techbuilder.DeclareAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Get connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found");

// Add DeclareAPI services
builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess(sp => new DapperDataAccess(
        connectionString,
        DatabaseProvider.PostgreSQL
    ));
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Development middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// DeclareAPI middleware
app.UseDeclareApiObservability();
app.UseDeclareApiPolicies();

// Map endpoints
app.MapDeclareApi();
app.MapDeclareApiHealthChecks();

app.Run();
```

### Step 3: Configure appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Step 4: Run Your API

```bash
dotnet run
```

Visit `http://localhost:5000/swagger` to see your API documentation.

## Environment Variables

DeclareAPI supports environment variable substitution in YAML:

```yaml
database:
  connection: "${DB_CONNECTION}"  # Reads from environment
```

You can set environment variables in:
- `.env` file (with dotenv)
- `launchSettings.json`
- System environment
- Docker/Kubernetes secrets

## Next Steps

- [Configuration Reference](02-configuration.md) - Learn all YAML options
- [Custom Handlers](03-custom-handlers.md) - Escape to C# when needed
- [Authorization](04-authorization.md) - Secure your endpoints
- [Rate Limiting & Caching](05-policies.md) - Performance features
