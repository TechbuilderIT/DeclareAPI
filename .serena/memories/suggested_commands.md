# Suggested Commands for DeclareAPI Development

## Build Commands
```bash
# Navigate to solution directory
cd Techbuilder.DeclareAPI

# Build solution
dotnet build

# Build specific project
dotnet build Techbuilder.DeclareAPI.Core

# Clean and rebuild
dotnet clean && dotnet build
```

## Test Commands
```bash
# Run all tests
cd Techbuilder.DeclareAPI
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run single test by name
dotnet test --filter "FullyQualifiedName~ConfigLoaderTests"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Sample Project
```bash
# Start PostgreSQL with sample data
cd Techbuilder.DeclareAPI/Techbuilder.DeclareAPI.Sample
docker-compose up -d

# Run sample API
dotnet run
```

## Windows System Commands
- `dir` instead of `ls`
- `findstr` instead of `grep`
- `where` instead of `which`
- `type` instead of `cat`
- Use `\\` for path separators

## Git Commands
```bash
git status
git add .
git commit -m "message"
git push
```
