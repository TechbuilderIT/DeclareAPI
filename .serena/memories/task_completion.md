# Task Completion Checklist

## Before Marking Task Complete

1. **Build Verification**
   ```bash
   cd Techbuilder.DeclareAPI
   dotnet build
   ```
   - Ensure 0 errors, 0 warnings (or justified warnings)

2. **Run Tests**
   ```bash
   dotnet test
   ```
   - All tests must pass
   - Add new tests for new functionality

3. **Code Review**
   - Check for proper error handling
   - Verify nullable reference types
   - Ensure public APIs have XML documentation

4. **Update Documentation**
   - Update CLAUDE.md if adding new features
   - Update Key Components table
   - Update Development Status section

## Test Requirements
- Unit tests for new classes/methods
- Use FluentAssertions for readable assertions
- Mock external dependencies with Moq
- Test edge cases and error conditions
