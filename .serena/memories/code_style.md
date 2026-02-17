# Code Style and Conventions

## C# Conventions
- Use `PascalCase` for public members, classes, methods
- Use `_camelCase` for private fields with underscore prefix
- Use `camelCase` for local variables and parameters
- Prefer expression-bodied members for simple properties
- Use `record` for immutable data transfer objects
- Use nullable reference types (`?` suffix)
- Use required properties with `required` keyword

## File Organization
- One class per file (unless closely related)
- Namespace matches folder structure
- Group related classes in folders (e.g., `Abstractions/`, `Configuration/`)

## Documentation
- Use XML documentation for public APIs (`/// <summary>`)
- Keep comments concise and meaningful
- Avoid obvious comments

## Patterns Used
- Repository/Data Access pattern with `IDataAccess`
- Custom Handler pattern with `ICustomHandler<TRequest, TResponse>`
- Options pattern for configuration
- Extension methods for service registration
- Middleware pattern for cross-cutting concerns

## Testing Conventions
- Use xUnit with `[Fact]` and `[Theory]` attributes
- Use FluentAssertions for assertions (`.Should()`)
- Use Moq for mocking
- Follow Arrange-Act-Assert pattern
- Name tests descriptively: `MethodName_Scenario_ExpectedResult`

## YAML Configuration
- Use snake_case for YAML keys
- Support environment variable expansion: `${VAR_NAME}`
- Validate configuration on load
