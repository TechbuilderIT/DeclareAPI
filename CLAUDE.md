# CLAUDE.md

## Regras de Ferramentas - OBRIGATÓRIO

### Serena MCP - USO OBRIGATÓRIO

**Para TODAS as tarefas de código utilizar OBRIGATORIAMENTE o Serena MCP:**

| Operação | Ferramenta Serena | NÃO usar |
|----------|-------------------|----------|
| Leitura de arquivos de código | `mcp__serena__search_for_pattern`, `mcp__serena__find_symbol` | `Read` |
| Busca de símbolos (classes, funções) | `mcp__serena__find_symbol`, `mcp__serena__get_symbols_overview` | `Grep` |
| Navegação no código/diretório | `mcp__serena__list_dir`, `mcp__serena__find_file` | `Glob` |
| Análise de estrutura | `mcp__serena__get_symbols_overview` | - |
| Edição de código | `mcp__serena__replace_symbol_body`, `mcp__serena__insert_after_symbol` | `Edit` |
| Referências de símbolos | `mcp__serena__find_referencing_symbols` | `Grep` |

### Exceções Permitidas

Usar ferramentas nativas apenas para:
- Arquivos de configuração não-código (CLAUDE.md, .env, .json config)
- Criação de novos arquivos que não existem
- Execução de comandos bash (git, npm, dotnet)

### Fluxo de Trabalho Recomendado

1. **Explorar** → `mcp__serena__list_dir`, `mcp__serena__find_file`
2. **Entender** → `mcp__serena__get_symbols_overview`, `mcp__serena__find_symbol`
3. **Analisar** → `mcp__serena__search_for_pattern`, `mcp__serena__find_referencing_symbols`
4. **Refletir** → `mcp__serena__think_about_collected_information`
5. **Planejar** → `mcp__serena__think_about_task_adherence`
6. **Editar** → `mcp__serena__replace_symbol_body`, `mcp__serena__insert_*`
7. **Verificar** → `mcp__serena__think_about_whether_you_are_done`

---

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Techbuilder.DeclareAPI** is a .NET 8 library that enables creating REST APIs from declarative YAML configuration, with seamless escape hatches to imperative C# code.

Core philosophy: *"Start with 40 lines of YAML, scale with C# — without rewriting anything."*

## Build Commands

```bash
# Build solution
cd Techbuilder.DeclareAPI
dotnet build

# Run tests
dotnet test

# Run single test
dotnet test --filter "FullyQualifiedName~ConfigLoaderTests"

# Run sample (requires PostgreSQL)
cd Techbuilder.DeclareAPI.Sample
docker-compose up -d  # Start PostgreSQL with sample data
dotnet run
```

## Project Structure

```
Techbuilder.DeclareAPI/
├── Techbuilder.DeclareAPI.Core/       # Interfaces, models, ConfigLoader
│   ├── Abstractions/                   # IDataAccess, ICustomHandler
│   ├── Configuration/                  # Config models, ConfigLoader
│   └── Models/                         # PagedResult, ApiError
├── Techbuilder.DeclareAPI/            # Main engine
│   ├── Routing/                        # RouteGenerator
│   └── Extensions/                     # AddDeclareApi, MapDeclareApi
├── Techbuilder.DeclareAPI.Dapper/     # Dapper implementation
├── Techbuilder.DeclareAPI.Tests/      # Unit tests
├── Techbuilder.DeclareAPI.Sample/     # Sample API project
│   ├── declareapi.yaml                 # Sample configuration
│   ├── sql/init.sql                    # PostgreSQL schema
│   └── docker-compose.yml              # PostgreSQL container
└── schemas/                            # JSON Schema for config validation
```

## Key Components

| Component | File | Purpose |
|-----------|------|---------|
| `ConfigLoader` | Core/Configuration/ConfigLoader.cs | Parse YAML, expand env vars, validate |
| `DeclareApiConfig` | Core/Configuration/DeclareApiConfig.cs | Root config model |
| `EndpointConfig` | Core/Configuration/EndpointConfig.cs | Endpoint, Source, Field, Filter models |
| `IDataAccess` | Core/Abstractions/IDataAccess.cs | Data access abstraction |
| `DapperDataAccess` | Dapper/DapperDataAccess.cs | Dapper + PostgreSQL implementation |
| `RouteGenerator` | Routing/RouteGenerator.cs | Generates Minimal API endpoints |
| `DynamicValidator` | Core/Validation/DynamicValidator.cs | Runtime FluentValidation from config |
| `FilterQueryBuilder` | Core/Query/FilterQueryBuilder.cs | Dynamic WHERE clause builder |
| `OpenApiSchemaGenerator` | OpenApi/OpenApiSchemaGenerator.cs | Generate OpenAPI schemas from config |
| `CorrelationIdMiddleware` | Observability/CorrelationIdMiddleware.cs | X-Correlation-ID tracking |
| `RequestLoggingMiddleware` | Observability/RequestLoggingMiddleware.cs | Structured request/response logging |
| `DeclareApiHealthCheck` | Observability/DeclareApiHealthCheck.cs | Database health verification |
| `ServiceCollectionExtensions` | Extensions/ServiceCollectionExtensions.cs | `AddDeclareApi()` |
| `EndpointRouteBuilderExtensions` | Extensions/EndpointRouteBuilderExtensions.cs | `MapDeclareApi()`, `MapDeclareApiHealthChecks()` |
| `ApplicationBuilderExtensions` | Extensions/EndpointRouteBuilderExtensions.cs | `UseDeclareApiObservability()`, `UseDeclareApiPolicies()` |
| `ICustomHandler<TRequest, TResponse>` | Core/Abstractions/ICustomHandler.cs | Custom handler interface |
| `HandlerNameAttribute` | Core/Abstractions/HandlerNameAttribute.cs | Handler naming for YAML config |
| `HandlerRegistry` | Handlers/HandlerRegistry.cs | Handler discovery and resolution |
| `RateLimitingExtensions` | RateLimiting/RateLimitingExtensions.cs | Rate limiting policies |
| `CachingExtensions` | Caching/CachingExtensions.cs | Output caching policies |

## Usage Pattern

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess(sp => new DapperDataAccess(connectionString, DatabaseProvider.PostgreSQL));
    options.ScanHandlersFrom<Program>();  // Optional: scan for custom handlers
    // Observability options (all enabled by default)
    options.EnableRequestLogging = true;
    options.EnableCorrelationId = true;
    options.EnableHealthChecks = true;
    // Policy options (all enabled by default)
    options.EnableRateLimiting = true;
    options.EnableCaching = true;
});

var app = builder.Build();

// Add observability middleware early in the pipeline
app.UseDeclareApiObservability();

// Add authentication/authorization here if needed
// app.UseAuthentication();
// app.UseAuthorization();

// Add rate limiting and caching after auth
app.UseDeclareApiPolicies();

app.MapDeclareApi();
app.MapDeclareApiHealthChecks(); // Maps /health and /health/ready
app.Run();
```

## YAML Configuration Format

```yaml
version: "1.0"
database:
  provider: postgresql
  connection: "${DB_CONNECTION}"
settings:
  base_path: /api
  default_page_size: 25
entities:
  Patient:
    endpoints:
      list:
        method: GET
        path: /patients
        source: { type: view, name: vw_patients }
        filters:
          - { field: name, operator: contains }
        paginated: true
        cache:
          duration: 300
          vary_by_query: true
      create:
        method: POST
        path: /patients
        source: { type: function, name: sp_create_patient }
        fields:
          - { name: p_name, type: string, required: true, max: 200 }
        returns: uuid
        authorize: true
        rate_limit:
          limit: 10
          window: 60
      admin_delete:
        method: DELETE
        path: /patients/{id}
        handler: DeletePatientHandler  # Custom C# handler
        policy: admin_only
        roles:
          - admin
          - manager
```

## Development Status

**Phase 1 — Foundation: COMPLETE**
- ConfigLoader with YAML parsing and validation
- IDataAccess abstraction with Dapper implementation
- RouteGenerator for all HTTP methods (GET, POST, PUT, PATCH, DELETE)
- Sample project with PostgreSQL + Docker
- Unit tests for ConfigLoader

**Phase 2 — CRUD Completo: COMPLETE**
- `DynamicValidator` with FluentValidation for field validation (required, max/min, pattern, types)
- `FilterQueryBuilder` with all operators (equals, contains, starts_with, ends_with, between, in, gt, gte, lt, lte)
- `OpenApiSchemaGenerator` for automatic schema generation from config
- Dynamic sorting with validation against allowed fields

**Phase 3 — Observability: COMPLETE**
- `CorrelationIdMiddleware` for request tracing with X-Correlation-ID headers
- `RequestLoggingMiddleware` for structured logging of requests/responses
- `ICorrelationIdAccessor` for thread-safe correlation ID access (AsyncLocal)
- `IRequestLogger` abstraction with `DefaultRequestLogger` implementation
- `DeclareApiHealthCheck` for database connectivity verification
- `DeclareApiConfigHealthCheck` for configuration validation
- `UseDeclareApiObservability()` middleware extension
- `MapDeclareApiHealthChecks()` endpoint extension
- Configurable options: `EnableRequestLogging`, `EnableCorrelationId`, `EnableHealthChecks`
- 60 unit tests passing

**Phase 4 — Advanced Features: COMPLETE**
- `ICustomHandler<TRequest, TResponse>` interface for imperative C# escape hatch
- `HandlerNameAttribute` for naming handlers in YAML config
- `HandlerRegistry` for assembly scanning and handler resolution
- `HandlerInvoker` for invoking handlers from HTTP context
- Authorization support: `authorize`, `policy`, `roles` in YAML config
- `RateLimitingExtensions` for fixed-window rate limiting policies
- `CachingExtensions` for output caching with vary-by options
- `UseDeclareApiPolicies()` middleware extension
- Configurable options: `EnableRateLimiting`, `EnableCaching`
- 85 unit tests passing

**Next: Phase 5 — Polish & Documentation**
- Comprehensive documentation
- More sample applications
- Performance benchmarks
- NuGet package publishing

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| YamlDotNet | 16.x | YAML parsing |
| Dapper | 2.x | Data access |
| Npgsql | 10.x | PostgreSQL driver |
| FluentValidation | 12.x | Input validation |
| Swashbuckle.AspNetCore | 6.x | OpenAPI/Swagger |

## Design Principles

1. **Library, not platform** — NuGet package in user's project
2. **Declarative by default, imperative by exception** — 80% config, 20% C#
3. **Database as first-class citizen** — Direct reference to views, SPs, functions
4. **Frictionless escape hatch** — Add `handler:` to switch to C#
5. **Architecture agnostic** — Works with Minimal APIs, MVC, hexagonal
