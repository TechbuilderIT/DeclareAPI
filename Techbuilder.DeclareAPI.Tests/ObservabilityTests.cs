using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Techbuilder.DeclareAPI.Core.Observability;
using Techbuilder.DeclareAPI.Observability;

namespace Techbuilder.DeclareAPI.Tests;

public class CorrelationIdAccessorTests
{
    [Fact]
    public void CorrelationId_Initially_ReturnsNull()
    {
        // Arrange
        var accessor = new CorrelationIdAccessor();

        // Act & Assert
        accessor.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void SetCorrelationId_SetsValue()
    {
        // Arrange
        var accessor = new CorrelationIdAccessor();
        var correlationId = "test-correlation-id";

        // Act
        accessor.SetCorrelationId(correlationId);

        // Assert
        accessor.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task SetCorrelationId_IsAsyncLocalScoped()
    {
        // Arrange
        var accessor = new CorrelationIdAccessor();

        // Act
        var task1CorrelationId = "";
        var task2CorrelationId = "";

        var task1 = Task.Run(() =>
        {
            accessor.SetCorrelationId("task1-id");
            Thread.Sleep(50); // Simulate some work
            task1CorrelationId = accessor.CorrelationId!;
        });

        var task2 = Task.Run(() =>
        {
            accessor.SetCorrelationId("task2-id");
            Thread.Sleep(50); // Simulate some work
            task2CorrelationId = accessor.CorrelationId!;
        });

        await Task.WhenAll(task1, task2);

        // Assert - each task should have its own correlation ID
        task1CorrelationId.Should().Be("task1-id");
        task2CorrelationId.Should().Be("task2-id");
    }
}

public class RequestLogContextTests
{
    [Fact]
    public void RequestLogContext_RequiredProperties_AreSet()
    {
        // Arrange & Act
        var context = new RequestLogContext
        {
            CorrelationId = "abc123",
            Method = "GET",
            Path = "/api/test"
        };

        // Assert
        context.CorrelationId.Should().Be("abc123");
        context.Method.Should().Be("GET");
        context.Path.Should().Be("/api/test");
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RequestLogContext_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var context = new RequestLogContext
        {
            CorrelationId = "abc123",
            Method = "GET",
            Path = "/api/test"
        };

        // Assert
        context.QueryString.Should().BeNull();
        context.ClientIp.Should().BeNull();
        context.UserAgent.Should().BeNull();
    }
}

public class ResponseLogContextTests
{
    [Fact]
    public void ResponseLogContext_RequiredProperties_AreSet()
    {
        // Arrange & Act
        var context = new ResponseLogContext
        {
            StatusCode = 200,
            DurationMs = 150
        };

        // Assert
        context.StatusCode.Should().Be(200);
        context.DurationMs.Should().Be(150);
    }

    [Fact]
    public void ResponseLogContext_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var context = new ResponseLogContext
        {
            StatusCode = 200,
            DurationMs = 150
        };

        // Assert
        context.ContentLength.Should().BeNull();
        context.ContentType.Should().BeNull();
    }
}

public class DefaultRequestLoggerTests
{
    private readonly Mock<ILogger<DefaultRequestLogger>> _mockLogger;
    private readonly DefaultRequestLogger _logger;

    public DefaultRequestLoggerTests()
    {
        _mockLogger = new Mock<ILogger<DefaultRequestLogger>>();
        _logger = new DefaultRequestLogger(_mockLogger.Object);
    }

    [Fact]
    public void LogRequestStart_LogsInformation()
    {
        // Arrange
        var context = new RequestLogContext
        {
            CorrelationId = "test-id",
            Method = "GET",
            Path = "/api/test",
            ClientIp = "127.0.0.1"
        };

        // Act
        _logger.LogRequestStart(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogRequestEnd_WithSuccessStatus_LogsInformation()
    {
        // Arrange
        var requestContext = new RequestLogContext
        {
            CorrelationId = "test-id",
            Method = "GET",
            Path = "/api/test"
        };

        var responseContext = new ResponseLogContext
        {
            StatusCode = 200,
            DurationMs = 50
        };

        // Act
        _logger.LogRequestEnd(requestContext, responseContext);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogRequestEnd_With4xxStatus_LogsWarning()
    {
        // Arrange
        var requestContext = new RequestLogContext
        {
            CorrelationId = "test-id",
            Method = "GET",
            Path = "/api/test"
        };

        var responseContext = new ResponseLogContext
        {
            StatusCode = 404,
            DurationMs = 50
        };

        // Act
        _logger.LogRequestEnd(requestContext, responseContext);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogRequestEnd_With5xxStatus_LogsError()
    {
        // Arrange
        var requestContext = new RequestLogContext
        {
            CorrelationId = "test-id",
            Method = "GET",
            Path = "/api/test"
        };

        var responseContext = new ResponseLogContext
        {
            StatusCode = 500,
            DurationMs = 50
        };

        // Act
        _logger.LogRequestEnd(requestContext, responseContext);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogRequestError_LogsErrorWithException()
    {
        // Arrange
        var requestContext = new RequestLogContext
        {
            CorrelationId = "test-id",
            Method = "GET",
            Path = "/api/test"
        };

        var exception = new InvalidOperationException("Test exception");

        // Act
        _logger.LogRequestError(requestContext, exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithNoHeader_GeneratesCorrelationId()
    {
        // Arrange
        var correlationIdAccessor = new CorrelationIdAccessor();
        string? capturedCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(next: (innerContext) =>
        {
            capturedCorrelationId = correlationIdAccessor.CorrelationId;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context, correlationIdAccessor);

        // Assert
        capturedCorrelationId.Should().NotBeNullOrEmpty();
        // Correlation ID should be stored in HttpContext.Items
        context.Items[CorrelationIdMiddleware.CorrelationIdHeaderName].Should().Be(capturedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WithExistingHeader_UsesProvidedCorrelationId()
    {
        // Arrange
        var correlationIdAccessor = new CorrelationIdAccessor();
        var providedId = "my-custom-correlation-id";
        string? capturedCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(next: (innerContext) =>
        {
            capturedCorrelationId = correlationIdAccessor.CorrelationId;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = providedId;

        // Act
        await middleware.InvokeAsync(context, correlationIdAccessor);

        // Assert
        capturedCorrelationId.Should().Be(providedId);
    }

    [Fact]
    public async Task InvokeAsync_StoresCorrelationIdInHttpContextItems()
    {
        // Arrange
        var correlationIdAccessor = new CorrelationIdAccessor();
        var middleware = new CorrelationIdMiddleware(next: (innerContext) => Task.CompletedTask);

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "test-id";

        // Act
        await middleware.InvokeAsync(context, correlationIdAccessor);

        // Assert
        context.Items[CorrelationIdMiddleware.CorrelationIdHeaderName].Should().Be("test-id");
    }

    [Fact]
    public async Task InvokeAsync_GeneratedId_HasExpectedFormat()
    {
        // Arrange
        var correlationIdAccessor = new CorrelationIdAccessor();
        string? capturedCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(next: (innerContext) =>
        {
            capturedCorrelationId = correlationIdAccessor.CorrelationId;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context, correlationIdAccessor);

        // Assert - format should be timestamp-random (e.g., "19c6936ad92-fc8f076b")
        capturedCorrelationId.Should().MatchRegex(@"^[0-9a-f]+-[0-9a-f]{8}$");
    }
}
