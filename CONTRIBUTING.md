# Contributing to DeclareAPI

Thank you for your interest in contributing to DeclareAPI! This document provides guidelines and information for contributors.

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker (for PostgreSQL)
- Visual Studio 2022, VS Code, or Rider

### Setup

1. Clone the repository
```bash
git clone https://github.com/techbuilder/DeclareAPI.git
cd DeclareAPI/Techbuilder.DeclareAPI
```

2. Build the solution
```bash
dotnet build
```

3. Run tests
```bash
dotnet test
```

4. Run the sample (optional)
```bash
cd Techbuilder.DeclareAPI.Sample
docker-compose up -d
dotnet run
```

## Development Workflow

### Branch Naming

- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation changes
- `refactor/description` - Code refactoring

### Commit Messages

Follow conventional commits:

```
feat: add rate limiting support
fix: resolve caching issue with query params
docs: update README with new examples
test: add tests for authorization
refactor: simplify ConfigLoader logic
```

### Pull Requests

1. Create a feature branch from `main`
2. Make your changes
3. Write/update tests
4. Ensure all tests pass
5. Update documentation if needed
6. Submit a pull request

## Code Style

### C# Guidelines

- Use `var` when the type is obvious
- Use meaningful names (no abbreviations)
- One class per file
- XML documentation for public APIs
- Async methods end with `Async`

### YAML Guidelines

- Use 2-space indentation
- Quote strings with special characters
- Use lowercase with underscores for keys

## Testing

### Running Tests

```bash
dotnet test
```

### Running Specific Tests

```bash
dotnet test --filter "FullyQualifiedName~ConfigLoaderTests"
```

### Writing Tests

- Use xUnit
- Use FluentAssertions
- Use Moq for mocking
- Follow AAA pattern (Arrange, Act, Assert)

Example:
```csharp
[Fact]
public void ConfigLoader_WithValidYaml_ParsesCorrectly()
{
    // Arrange
    var yaml = "version: '1.0'...";
    var loader = new ConfigLoader();

    // Act
    var config = loader.LoadFromYaml(yaml);

    // Assert
    config.Version.Should().Be("1.0");
}
```

## Project Structure

```
Techbuilder.DeclareAPI/
├── Techbuilder.DeclareAPI.Core/     # Core interfaces and models
├── Techbuilder.DeclareAPI/          # Main library
├── Techbuilder.DeclareAPI.Dapper/   # Dapper implementation
├── Techbuilder.DeclareAPI.Tests/    # Unit tests
└── Techbuilder.DeclareAPI.Sample/   # Sample application
```

## Adding Features

### New Data Source Types

1. Add source type to `SourceType` enum
2. Update `RouteGenerator` to handle new type
3. Add tests
4. Update documentation

### New Filter Operators

1. Add operator to `FilterOperator` enum
2. Update `FilterQueryBuilder`
3. Add tests
4. Update documentation

### New Validation Rules

1. Add rule to `DynamicValidator`
2. Update field configuration model
3. Add tests
4. Update documentation

## Questions?

Open an issue or start a discussion on GitHub.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
