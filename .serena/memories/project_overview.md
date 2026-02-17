# DeclareAPI Project Overview

## Purpose
Techbuilder.DeclareAPI is a .NET 8 library that enables creating REST APIs from declarative YAML configuration, with seamless escape hatches to imperative C# code.

Core philosophy: "Start with 40 lines of YAML, scale with C# — without rewriting anything."

## Tech Stack
- .NET 8.0
- ASP.NET Core Minimal APIs
- YamlDotNet (YAML parsing)
- Dapper (data access)
- Npgsql (PostgreSQL driver)
- FluentValidation (input validation)
- Swashbuckle.AspNetCore (OpenAPI/Swagger)
- xUnit + FluentAssertions + Moq (testing)

## Project Structure
```
DeclareAPI/
├── Techbuilder.DeclareAPI.Core/       # Interfaces, models, ConfigLoader
│   ├── Abstractions/                   # IDataAccess, ICustomHandler
│   ├── Configuration/                  # Config models, ConfigLoader
│   ├── Models/                         # PagedResult, ApiError
│   ├── Observability/                  # ICorrelationIdAccessor, IRequestLogger
│   ├── Query/                          # FilterQueryBuilder
│   └── Validation/                     # DynamicValidator
├── Techbuilder.DeclareAPI/            # Main engine
│   ├── Routing/                        # RouteGenerator
│   ├── Observability/                  # Middleware implementations
│   ├── OpenApi/                        # OpenApiSchemaGenerator
│   └── Extensions/                     # AddDeclareApi, MapDeclareApi
├── Techbuilder.DeclareAPI.Dapper/     # Dapper implementation
├── Techbuilder.DeclareAPI.Tests/      # Unit tests (60+ tests)
├── Techbuilder.DeclareAPI.Sample/     # Sample API project
└── schemas/                            # JSON Schema for config validation
```

## Development Phases
- Phase 1 (Foundation): COMPLETE - ConfigLoader, IDataAccess, RouteGenerator
- Phase 2 (CRUD): COMPLETE - DynamicValidator, FilterQueryBuilder, OpenApiSchemaGenerator
- Phase 3 (Observability): COMPLETE - Correlation ID, Request Logging, Health Checks
- Phase 4 (Advanced): IN PROGRESS - Custom Handlers, Authorization, Rate Limiting, Caching
