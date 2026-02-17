using FluentAssertions;
using Techbuilder.DeclareAPI.Core.Abstractions;
using Techbuilder.DeclareAPI.Handlers;

namespace Techbuilder.DeclareAPI.Tests;

#region Test Handlers

public record TestRequest
{
    public string Name { get; init; } = string.Empty;
    public int Value { get; init; }
}

public record TestResponse
{
    public string Result { get; init; } = string.Empty;
}

public class TestHandler : ICustomHandler<TestRequest, TestResponse>
{
    public Task<TestResponse> HandleAsync(TestRequest request, IDataAccess data, CancellationToken ct)
    {
        return Task.FromResult(new TestResponse
        {
            Result = $"Processed: {request.Name} with value {request.Value}"
        });
    }
}

[HandlerName("CustomName")]
public class CustomNamedHandler : ICustomHandler<TestRequest, TestResponse>
{
    public Task<TestResponse> HandleAsync(TestRequest request, IDataAccess data, CancellationToken ct)
    {
        return Task.FromResult(new TestResponse { Result = "Custom named handler" });
    }
}

public class AnotherTestHandler : ICustomHandler<TestRequest, TestResponse>
{
    public Task<TestResponse> HandleAsync(TestRequest request, IDataAccess data, CancellationToken ct)
    {
        return Task.FromResult(new TestResponse { Result = "Another" });
    }
}

#endregion

public class HandlerNameAttributeTests
{
    [Fact]
    public void Constructor_WithValidName_SetsName()
    {
        // Arrange & Act
        var attribute = new HandlerNameAttribute("MyHandler");

        // Assert
        attribute.Name.Should().Be("MyHandler");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        var act = () => new HandlerNameAttribute(invalidName!);
        act.Should().Throw<ArgumentException>();
    }
}

public class HandlerRegistryTests
{
    [Fact]
    public void GetHandler_ByClassName_ReturnsHandler()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(TestHandler).Assembly });

        // Act
        var metadata = registry.GetHandler("Test");

        // Assert
        metadata.Should().NotBeNull();
        metadata!.HandlerType.Should().Be(typeof(TestHandler));
        metadata.RequestType.Should().Be(typeof(TestRequest));
        metadata.ResponseType.Should().Be(typeof(TestResponse));
    }

    [Fact]
    public void GetHandler_ByCustomName_ReturnsHandler()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(CustomNamedHandler).Assembly });

        // Act
        var metadata = registry.GetHandler("CustomName");

        // Assert
        metadata.Should().NotBeNull();
        metadata!.HandlerType.Should().Be(typeof(CustomNamedHandler));
    }

    [Fact]
    public void GetHandler_CaseInsensitive_ReturnsHandler()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(TestHandler).Assembly });

        // Act
        var metadata = registry.GetHandler("TEST");

        // Assert
        metadata.Should().NotBeNull();
        metadata!.Name.Should().Be("Test");
    }

    [Fact]
    public void GetHandler_NonExistent_ReturnsNull()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(TestHandler).Assembly });

        // Act
        var metadata = registry.GetHandler("NonExistent");

        // Assert
        metadata.Should().BeNull();
    }

    [Fact]
    public void HasHandler_WhenExists_ReturnsTrue()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(TestHandler).Assembly });

        // Act
        var exists = registry.HasHandler("Test");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void HasHandler_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(TestHandler).Assembly });

        // Act
        var exists = registry.HasHandler("NonExistent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetAllHandlers_ReturnsAllRegisteredHandlers()
    {
        // Arrange
        var registry = new HandlerRegistry(new[] { typeof(TestHandler).Assembly });

        // Act
        var handlers = registry.GetAllHandlers();

        // Assert
        handlers.Should().NotBeEmpty();
        handlers.Should().Contain(h => h.Name == "Test");
        handlers.Should().Contain(h => h.Name == "CustomName");
        handlers.Should().Contain(h => h.Name == "AnotherTest");
    }

    [Fact]
    public void Constructor_WithEmptyAssemblies_CreatesEmptyRegistry()
    {
        // Arrange & Act
        var registry = new HandlerRegistry(Array.Empty<System.Reflection.Assembly>());

        // Assert
        registry.GetAllHandlers().Should().BeEmpty();
    }
}

public class HandlerMetadataTests
{
    [Fact]
    public void HandlerMetadata_RequiredProperties_AreSet()
    {
        // Arrange & Act
        var metadata = new HandlerMetadata
        {
            Name = "TestHandler",
            HandlerType = typeof(TestHandler),
            RequestType = typeof(TestRequest),
            ResponseType = typeof(TestResponse)
        };

        // Assert
        metadata.Name.Should().Be("TestHandler");
        metadata.HandlerType.Should().Be(typeof(TestHandler));
        metadata.RequestType.Should().Be(typeof(TestRequest));
        metadata.ResponseType.Should().Be(typeof(TestResponse));
    }
}
