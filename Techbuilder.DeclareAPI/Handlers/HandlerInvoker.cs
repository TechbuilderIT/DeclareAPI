using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Techbuilder.DeclareAPI.Core.Abstractions;

namespace Techbuilder.DeclareAPI.Handlers;

/// <summary>
/// Invokes custom handlers resolved from the service provider.
/// </summary>
public class HandlerInvoker
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHandlerRegistry _registry;

    public HandlerInvoker(IServiceProvider serviceProvider, IHandlerRegistry registry)
    {
        _serviceProvider = serviceProvider;
        _registry = registry;
    }

    /// <summary>
    /// Invokes a custom handler by name.
    /// </summary>
    public async Task<IResult> InvokeAsync(
        string handlerName,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var metadata = _registry.GetHandler(handlerName);
        if (metadata == null)
        {
            return Results.NotFound(new { error = $"Handler '{handlerName}' not found." });
        }

        // Get handler instance from DI
        var handler = _serviceProvider.GetRequiredService(metadata.HandlerType);

        // Build request object from HTTP context
        var request = await BuildRequestAsync(metadata.RequestType, httpContext, ct);

        // Get the HandleAsync method
        var handleMethod = metadata.HandlerType.GetMethod("HandleAsync");
        if (handleMethod == null)
        {
            return Results.Problem($"Handler '{handlerName}' does not have HandleAsync method.");
        }

        // Get IDataAccess from DI
        var dataAccess = _serviceProvider.GetRequiredService<IDataAccess>();

        // Invoke the handler
        var task = (Task)handleMethod.Invoke(handler, new object[] { request!, dataAccess, ct })!;
        await task.ConfigureAwait(false);

        // Get the result from the task
        var resultProperty = task.GetType().GetProperty("Result");
        var response = resultProperty!.GetValue(task);

        return Results.Ok(response);
    }

    private static async Task<object?> BuildRequestAsync(
        Type requestType,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Try to deserialize from body for POST/PUT/PATCH
        var method = httpContext.Request.Method.ToUpperInvariant();
        if (method is "POST" or "PUT" or "PATCH")
        {
            if (httpContext.Request.ContentLength > 0)
            {
                try
                {
                    var request = await JsonSerializer.DeserializeAsync(
                        httpContext.Request.Body,
                        requestType,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        ct);
                    
                    if (request != null)
                    {
                        // Merge route values into request
                        MergeRouteValues(request, httpContext.Request.RouteValues);
                        return request;
                    }
                }
                catch (JsonException)
                {
                    // Fall through to create from route/query values
                }
            }
        }

        // Create request from route values and query parameters
        return CreateRequestFromContext(requestType, httpContext);
    }

    private static object? CreateRequestFromContext(Type requestType, HttpContext httpContext)
    {
        var request = Activator.CreateInstance(requestType);
        if (request == null) return null;

        var properties = requestType.GetProperties()
            .Where(p => p.CanWrite);

        foreach (var property in properties)
        {
            var value = GetValueFromContext(property.Name, httpContext);
            if (value != null)
            {
                var convertedValue = ConvertValue(value, property.PropertyType);
                if (convertedValue != null)
                {
                    property.SetValue(request, convertedValue);
                }
            }
        }

        return request;
    }

    private static string? GetValueFromContext(string propertyName, HttpContext httpContext)
    {
        // Check route values first
        if (httpContext.Request.RouteValues.TryGetValue(propertyName, out var routeValue))
        {
            return routeValue?.ToString();
        }

        // Check query string (case-insensitive)
        var queryValue = httpContext.Request.Query
            .FirstOrDefault(q => q.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            .Value
            .FirstOrDefault();

        return queryValue;
    }

    private static void MergeRouteValues(object request, RouteValueDictionary routeValues)
    {
        var properties = request.GetType().GetProperties().Where(p => p.CanWrite);

        foreach (var property in properties)
        {
            if (routeValues.TryGetValue(property.Name, out var value) && value != null)
            {
                var convertedValue = ConvertValue(value.ToString()!, property.PropertyType);
                if (convertedValue != null)
                {
                    property.SetValue(request, convertedValue);
                }
            }
        }
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (underlyingType == typeof(string))
                return value;
            if (underlyingType == typeof(int))
                return int.Parse(value);
            if (underlyingType == typeof(long))
                return long.Parse(value);
            if (underlyingType == typeof(Guid))
                return Guid.Parse(value);
            if (underlyingType == typeof(bool))
                return bool.Parse(value);
            if (underlyingType == typeof(decimal))
                return decimal.Parse(value);
            if (underlyingType == typeof(double))
                return double.Parse(value);
            if (underlyingType == typeof(DateTime))
                return DateTime.Parse(value);
            if (underlyingType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(value);

            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return null;
        }
    }
}
