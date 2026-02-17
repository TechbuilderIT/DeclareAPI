using FluentAssertions;
using Techbuilder.DeclareAPI.Core.Configuration;
using Techbuilder.DeclareAPI.Core.Validation;

namespace Techbuilder.DeclareAPI.Tests;

public class DynamicValidatorTests
{
    private readonly DynamicValidator _validator = new();

    [Fact]
    public void Validate_RequiredFieldMissing_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "name", Type = "string", Required = true }
        };
        var body = new Dictionary<string, object?>();

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("'name' is required"));
    }

    [Fact]
    public void Validate_RequiredFieldPresent_ReturnsSuccess()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "name", Type = "string", Required = true }
        };
        var body = new Dictionary<string, object?> { ["name"] = "John" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StringExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "name", Type = "string", Max = 5 }
        };
        var body = new Dictionary<string, object?> { ["name"] = "TooLongName" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("must not exceed 5"));
    }

    [Fact]
    public void Validate_StringBelowMinLength_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "name", Type = "string", Min = 3 }
        };
        var body = new Dictionary<string, object?> { ["name"] = "AB" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("at least 3"));
    }

    [Fact]
    public void Validate_StringMatchesPattern_ReturnsSuccess()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "cpf", Type = "string", Pattern = @"^\d{11}$" }
        };
        var body = new Dictionary<string, object?> { ["cpf"] = "12345678901" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StringDoesNotMatchPattern_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "cpf", Type = "string", Pattern = @"^\d{11}$" }
        };
        var body = new Dictionary<string, object?> { ["cpf"] = "123-456-789" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("pattern"));
    }

    [Fact]
    public void Validate_IntegerExceedsMax_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "age", Type = "int", Max = 120 }
        };
        var body = new Dictionary<string, object?> { ["age"] = 150 };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("not exceed 120"));
    }

    [Fact]
    public void Validate_IntegerBelowMin_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "age", Type = "int", Min = 0 }
        };
        var body = new Dictionary<string, object?> { ["age"] = -5 };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("at least 0"));
    }

    [Fact]
    public void Validate_InvalidUuid_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "id", Type = "uuid" }
        };
        var body = new Dictionary<string, object?> { ["id"] = "not-a-uuid" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid UUID"));
    }

    [Fact]
    public void Validate_ValidUuid_ReturnsSuccess()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "id", Type = "uuid" }
        };
        var body = new Dictionary<string, object?> { ["id"] = "550e8400-e29b-41d4-a716-446655440000" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidDate_ReturnsError()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "birth_date", Type = "date" }
        };
        var body = new Dictionary<string, object?> { ["birth_date"] = "not-a-date" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid date"));
    }

    [Fact]
    public void Validate_ValidDate_ReturnsSuccess()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "birth_date", Type = "date" }
        };
        var body = new Dictionary<string, object?> { ["birth_date"] = "2020-03-15" };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OptionalFieldMissing_ReturnsSuccess()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "nickname", Type = "string", Required = false }
        };
        var body = new Dictionary<string, object?>();

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleFieldsWithErrors_ReturnsAllErrors()
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "name", Type = "string", Required = true },
            new() { Name = "age", Type = "int", Max = 120 }
        };
        var body = new Dictionary<string, object?> { ["age"] = 150 };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("yes", false)]
    [InlineData("1", false)]
    public void Validate_BoolValues_ValidatesCorrectly(string value, bool shouldBeValid)
    {
        // Arrange
        var fields = new List<FieldConfig>
        {
            new() { Name = "active", Type = "bool" }
        };
        var body = new Dictionary<string, object?> { ["active"] = value };

        // Act
        var result = _validator.Validate(body, fields);

        // Assert
        result.IsValid.Should().Be(shouldBeValid);
    }
}
