using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Techbuilder.DeclareAPI.Core.Abstractions;
using Techbuilder.DeclareAPI.Core.Configuration;
using Techbuilder.DeclareAPI.Core.Models;
using Techbuilder.DeclareAPI.Core.Validation;
using Techbuilder.DeclareAPI.Handlers;
using Techbuilder.DeclareAPI.OpenApi;

namespace Techbuilder.DeclareAPI.Routing;

/// <summary>
/// Generates Minimal API endpoints from DeclareAPI configuration.
/// </summary>
public class RouteGenerator
{
    private readonly DeclareApiConfig _config;
    private readonly IHandlerRegistry? _handlerRegistry;
    private readonly DynamicValidator _validator = new();
    private readonly OpenApiSchemaGenerator _openApiGenerator = new();

    public RouteGenerator(DeclareApiConfig config, IHandlerRegistry? handlerRegistry = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _handlerRegistry = handlerRegistry;
    }

    /// <summary>
    /// Maps all configured endpoints to the application.
    /// </summary>
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        foreach (var (entityName, entity) in _config.Entities)
        {
            foreach (var (endpointName, endpoint) in entity.Endpoints)
            {
                var fullPath = BuildFullPath(endpoint.Path);
                var tags = new[] { entityName };

                if (endpoint.IsCustomHandler)
                {
                    MapCustomHandlerEndpoint(app, entityName, endpointName, endpoint, fullPath, tags);
                    continue;
                }

                MapDeclarativeEndpoint(app, entityName, endpointName, endpoint, fullPath, tags);
            }
        }
    }

    private void MapDeclarativeEndpoint(
        IEndpointRouteBuilder app,
        string entityName,
        string endpointName,
        EndpointConfig endpoint,
        string fullPath,
        string[] tags)
    {
        var routeBuilder = endpoint.Method.ToUpperInvariant() switch
        {
            "GET" => MapGetEndpoint(app, endpoint, fullPath),
            "POST" => MapPostEndpoint(app, endpoint, fullPath),
            "PUT" => MapPutEndpoint(app, endpoint, fullPath),
            "PATCH" => MapPatchEndpoint(app, endpoint, fullPath),
            "DELETE" => MapDeleteEndpoint(app, endpoint, fullPath),
            _ => throw new NotSupportedException($"HTTP method {endpoint.Method} is not supported")
        };

        routeBuilder
            .WithName($"{entityName}_{endpointName}")
            .WithTags(tags)
            .WithOpenApi(operation =>
            {
                operation.Summary = GetOperationSummary(entityName, endpointName, endpoint);
                operation.Description = GetOperationDescription(entityName, endpoint);

                // Add filter parameters for GET paginated endpoints
                if (endpoint.Method == "GET" && endpoint.Paginated)
                {
                    // Add pagination parameters
                    var paginationParams = _openApiGenerator.GeneratePaginationParameters(
                        _config.Settings.DefaultPageSize,
                        _config.Settings.MaxPageSize);
                    foreach (var param in paginationParams)
                    {
                        operation.Parameters.Add(param);
                    }

                    // Add sort parameter
                    var sortParam = _openApiGenerator.GenerateSortParameter(endpoint.Sort);
                    if (sortParam != null)
                    {
                        operation.Parameters.Add(sortParam);
                    }

                    // Add filter parameters
                    if (endpoint.Filters != null)
                    {
                        var filterParams = _openApiGenerator.GenerateFilterParameters(endpoint.Filters);
                        foreach (var param in filterParams)
                        {
                            operation.Parameters.Add(param);
                        }
                    }
                }

                return operation;
            });

        // Apply authorization
        ApplyAuthorization(routeBuilder, endpoint);

        // Apply rate limiting
        ApplyRateLimiting(routeBuilder, endpoint);

        // Apply caching
        ApplyCaching(routeBuilder, endpoint);
    }

    private static void ApplyAuthorization(RouteHandlerBuilder routeBuilder, EndpointConfig endpoint)
    {
        if (!endpoint.RequiresAuthorization)
        {
            routeBuilder.AllowAnonymous();
            return;
        }

        if (!string.IsNullOrEmpty(endpoint.Policy))
        {
            // Named policy
            routeBuilder.RequireAuthorization(endpoint.Policy);
        }
        else if (endpoint.Roles?.Count > 0)
        {
            // Role-based authorization
            routeBuilder.RequireAuthorization(policy =>
                policy.RequireRole(endpoint.Roles.ToArray()));
        }
        else if (endpoint.Authorize)
        {
            // Simple authentication required
            routeBuilder.RequireAuthorization();
        }
    }

    private static void ApplyRateLimiting(RouteHandlerBuilder routeBuilder, EndpointConfig endpoint)
    {
        if (endpoint.RateLimit == null) return;

        var policyName = endpoint.RateLimit.Policy ??
            $"ratelimit_{endpoint.RateLimit.Limit}_{endpoint.RateLimit.Window}";

        routeBuilder.RequireRateLimiting(policyName);
    }

    private static void ApplyCaching(RouteHandlerBuilder routeBuilder, EndpointConfig endpoint)
    {
        if (endpoint.Cache == null) return;

        // Output caching is applied via policy name
        var policyName = $"cache_{endpoint.Cache.Duration}";
        routeBuilder.CacheOutput(policyName);
    }

    private void MapCustomHandlerEndpoint(
        IEndpointRouteBuilder app,
        string entityName,
        string endpointName,
        EndpointConfig endpoint,
        string fullPath,
        string[] tags)
    {
        if (_handlerRegistry == null)
        {
            throw new InvalidOperationException(
                $"Endpoint '{entityName}/{endpointName}' uses custom handler '{endpoint.Handler}' " +
                "but no handler registry is configured. Call options.ScanHandlersFrom() to enable custom handlers.");
        }

        var handlerName = endpoint.Handler!;
        var metadata = _handlerRegistry.GetHandler(handlerName);

        if (metadata == null)
        {
            throw new InvalidOperationException(
                $"Custom handler '{handlerName}' not found for endpoint '{entityName}/{endpointName}'. " +
                "Ensure the handler class exists and implements ICustomHandler<TRequest, TResponse>.");
        }

        RouteHandlerBuilder routeBuilder = endpoint.Method.ToUpperInvariant() switch
        {
            "GET" => app.MapGet(fullPath, async (HttpContext ctx, IServiceProvider sp, CancellationToken ct) =>
                await InvokeHandlerAsync(sp, metadata, ctx, ct)),
            "POST" => app.MapPost(fullPath, async (HttpContext ctx, IServiceProvider sp, CancellationToken ct) =>
                await InvokeHandlerAsync(sp, metadata, ctx, ct)),
            "PUT" => app.MapPut(fullPath, async (HttpContext ctx, IServiceProvider sp, CancellationToken ct) =>
                await InvokeHandlerAsync(sp, metadata, ctx, ct)),
            "PATCH" => app.MapPatch(fullPath, async (HttpContext ctx, IServiceProvider sp, CancellationToken ct) =>
                await InvokeHandlerAsync(sp, metadata, ctx, ct)),
            "DELETE" => app.MapDelete(fullPath, async (HttpContext ctx, IServiceProvider sp, CancellationToken ct) =>
                await InvokeHandlerAsync(sp, metadata, ctx, ct)),
            _ => throw new NotSupportedException($"HTTP method {endpoint.Method} is not supported")
        };

        routeBuilder
            .WithName($"{entityName}_{endpointName}")
            .WithTags(tags)
            .WithOpenApi(operation =>
            {
                operation.Summary = $"Custom: {handlerName}";
                operation.Description = $"Custom handler for {entityName}. Handler: {metadata.HandlerType.Name}";
                return operation;
            });

        // Apply authorization
        ApplyAuthorization(routeBuilder, endpoint);

        // Apply rate limiting
        ApplyRateLimiting(routeBuilder, endpoint);

        // Apply caching
        ApplyCaching(routeBuilder, endpoint);
    }

    private static async Task<IResult> InvokeHandlerAsync(
        IServiceProvider sp,
        HandlerMetadata metadata,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var invoker = new HandlerInvoker(sp, sp.GetRequiredService<IHandlerRegistry>());
        return await invoker.InvokeAsync(metadata.Name, httpContext, ct);
    }

    private static string GetOperationSummary(string entityName, string endpointName, EndpointConfig endpoint)
    {
        return endpoint.Method.ToUpperInvariant() switch
        {
            "GET" when endpoint.Paginated => $"List {entityName}",
            "GET" => $"Get {entityName} by ID",
            "POST" => $"Create {entityName}",
            "PUT" => $"Update {entityName}",
            "PATCH" => $"Partially update {entityName}",
            "DELETE" => $"Delete {entityName}",
            _ => $"{endpointName} {entityName}"
        };
    }

    private static string GetOperationDescription(string entityName, EndpointConfig endpoint)
    {
        var desc = endpoint.Method.ToUpperInvariant() switch
        {
            "GET" when endpoint.Paginated => $"Returns a paginated list of {entityName} with optional filtering and sorting.",
            "GET" => $"Returns a single {entityName} by its identifier.",
            "POST" => $"Creates a new {entityName} and returns the created resource.",
            "PUT" => $"Replaces an existing {entityName} with the provided data.",
            "PATCH" => $"Partially updates an existing {entityName}.",
            "DELETE" => $"Deletes an existing {entityName}.",
            _ => $"Operation on {entityName}"
        };

        if (endpoint.Source != null)
        {
            desc += $" (Source: {endpoint.Source.Type} '{endpoint.Source.Name}')";
        }

        return desc;
    }

    private RouteHandlerBuilder MapGetEndpoint(IEndpointRouteBuilder app, EndpointConfig endpoint, string path)
    {
        if (endpoint.Paginated)
        {
            return app.MapGet(path, async (
                HttpContext ctx,
                IDataAccess data,
                int page = 1,
                int pageSize = 25,
                string? orderBy = null,
                CancellationToken ct = default) =>
            {
                pageSize = Math.Min(pageSize, _config.Settings.MaxPageSize);
                var filterValues = ExtractFilterValues(ctx, endpoint);
                var orderByField = ValidateOrderBy(orderBy, endpoint.Sort);

                var result = await data.QueryPagedAsync<dynamic>(
                    endpoint.Source!.Name,
                    page,
                    pageSize,
                    endpoint.Filters,
                    filterValues,
                    orderByField,
                    ct);

                return Results.Ok(result);
            });
        }
        else
        {
            return app.MapGet(path, async (
                HttpContext ctx,
                IDataAccess data,
                CancellationToken ct = default) =>
            {
                var parameters = ExtractRouteAndQueryParameters(ctx, endpoint);

                var result = await data.QuerySingleAsync<dynamic>(
                    endpoint.Source!.Name,
                    parameters,
                    ct);

                return result != null
                    ? Results.Ok(result)
                    : Results.NotFound(ApiError.NotFound("Resource not found"));
            });
        }
    }

    private RouteHandlerBuilder MapPostEndpoint(IEndpointRouteBuilder app, EndpointConfig endpoint, string path)
    {
        return app.MapPost(path, async (
            HttpContext ctx,
            IDataAccess data,
            CancellationToken ct = default) =>
        {
            var body = await ReadAndValidateBody(ctx, endpoint);
            if (body.IsFailure)
            {
                if (body.HasValidationErrors)
                    return Results.BadRequest(body.ValidationErrors);
                return Results.BadRequest(body.Error);
            }

            if (endpoint.Source?.SourceType == SourceType.Procedure ||
                endpoint.Source?.SourceType == SourceType.Function)
            {
                if (!string.IsNullOrEmpty(endpoint.Returns))
                {
                    var result = await data.ExecuteScalarAsync<object>(
                        endpoint.Source.Name,
                        body.Value,
                        ct);
                    return Results.Created($"{path}/{result}", new { id = result });
                }

                await data.ExecuteAsync(endpoint.Source.Name, body.Value, ct);
                return Results.Created(path, null);
            }

            return Results.BadRequest(ApiError.BadRequest("POST requires a procedure or function source"));
        });
    }

    private RouteHandlerBuilder MapPutEndpoint(IEndpointRouteBuilder app, EndpointConfig endpoint, string path)
    {
        return app.MapPut(path, async (
            HttpContext ctx,
            IDataAccess data,
            CancellationToken ct = default) =>
        {
            var routeParams = ExtractRouteParameters(ctx, endpoint);
            var body = await ReadAndValidateBody(ctx, endpoint);
            if (body.IsFailure)
            {
                if (body.HasValidationErrors)
                    return Results.BadRequest(body.ValidationErrors);
                return Results.BadRequest(body.Error);
            }

            var parameters = MergeParameters(routeParams, body.Value);

            await data.ExecuteAsync(endpoint.Source!.Name, parameters, ct);
            return Results.NoContent();
        });
    }

    private RouteHandlerBuilder MapPatchEndpoint(IEndpointRouteBuilder app, EndpointConfig endpoint, string path)
    {
        // PATCH is similar to PUT for now
        return MapPutEndpoint(app, endpoint, path);
    }

    private RouteHandlerBuilder MapDeleteEndpoint(IEndpointRouteBuilder app, EndpointConfig endpoint, string path)
    {
        return app.MapDelete(path, async (
            HttpContext ctx,
            IDataAccess data,
            CancellationToken ct = default) =>
        {
            var parameters = ExtractRouteParameters(ctx, endpoint);

            await data.ExecuteAsync(endpoint.Source!.Name, parameters, ct);
            return Results.NoContent();
        });
    }

    private string BuildFullPath(string path)
    {
        var basePath = _config.Settings.BasePath.TrimEnd('/');
        var endpointPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{basePath}{endpointPath}";
    }

    private static Dictionary<string, object?> ExtractFilterValues(HttpContext ctx, EndpointConfig endpoint)
    {
        var filterValues = new Dictionary<string, object?>();

        if (endpoint.Filters != null)
        {
            foreach (var filter in endpoint.Filters)
            {
                var value = ctx.Request.Query[filter.Field].FirstOrDefault();
                if (!string.IsNullOrEmpty(value))
                {
                    filterValues[filter.Field] = value;
                }
            }
        }

        return filterValues;
    }

    private static object ExtractQueryParameters(HttpContext ctx, EndpointConfig endpoint)
    {
        var parameters = new Dictionary<string, object?>();

        if (endpoint.Filters != null)
        {
            foreach (var filter in endpoint.Filters)
            {
                var value = ctx.Request.Query[filter.Field].FirstOrDefault();
                if (!string.IsNullOrEmpty(value))
                {
                    parameters[filter.Field] = value;
                }
            }
        }

        return parameters;
    }

    private static object ExtractRouteParameters(HttpContext ctx, EndpointConfig endpoint)
    {
        var parameters = new Dictionary<string, object?>();

        if (endpoint.Params != null)
        {
            foreach (var param in endpoint.Params.Where(p => p.ParamSource == ParamSource.Route))
            {
                var value = ctx.Request.RouteValues[param.Name];
                if (value != null)
                {
                    parameters[param.Name] = ConvertValue(value.ToString()!, param.Type);
                }
            }
        }

        return parameters;
    }

    private static object ExtractRouteAndQueryParameters(HttpContext ctx, EndpointConfig endpoint)
    {
        var parameters = new Dictionary<string, object?>();

        if (endpoint.Params != null)
        {
            foreach (var param in endpoint.Params)
            {
                object? value = param.ParamSource switch
                {
                    ParamSource.Route => ctx.Request.RouteValues[param.Name],
                    ParamSource.Query => ctx.Request.Query[param.Name].FirstOrDefault(),
                    ParamSource.Header => ctx.Request.Headers[param.Name].FirstOrDefault(),
                    _ => null
                };

                if (value != null)
                {
                    parameters[param.Name] = ConvertValue(value.ToString()!, param.Type);
                }
            }
        }

        return parameters;
    }

    private async Task<OperationResult<Dictionary<string, object?>>> ReadAndValidateBody(
        HttpContext ctx,
        EndpointConfig endpoint)
    {
        try
        {
            var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object?>>();
            if (body == null)
                return OperationResult<Dictionary<string, object?>>.Failure(
                    ApiError.BadRequest("Request body is required"));

            // Validate against endpoint.Fields using FluentValidation
            if (endpoint.Fields != null && endpoint.Fields.Count > 0)
            {
                var validationResult = await _validator.ValidateAsync(body, endpoint.Fields, ctx.RequestAborted);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .Select(e => new ValidationErrorItem(e.PropertyName, e.ErrorMessage))
                        .ToList();
                    var problemDetails = ValidationProblemDetails.FromErrors(errors);
                    return OperationResult<Dictionary<string, object?>>.ValidationFailure(problemDetails);
                }
            }

            // Apply default values
            if (endpoint.Fields != null)
            {
                foreach (var field in endpoint.Fields.Where(f => f.Default != null))
                {
                    if (!body.ContainsKey(field.Name) || body[field.Name] == null)
                    {
                        body[field.Name] = field.Default;
                    }
                }
            }

            return OperationResult<Dictionary<string, object?>>.Success(body);
        }
        catch (Exception ex)
        {
            return OperationResult<Dictionary<string, object?>>.Failure(
                ApiError.BadRequest($"Invalid request body: {ex.Message}"));
        }
    }

    private static object ConvertValue(string value, string type)
    {
        return type.ToLowerInvariant() switch
        {
            "uuid" => Guid.Parse(value),
            "int" => int.Parse(value),
            "long" => long.Parse(value),
            "decimal" => decimal.Parse(value),
            "bool" => bool.Parse(value),
            "date" or "datetime" => DateTime.Parse(value),
            _ => value
        };
    }

    private static string? ValidateOrderBy(string? orderBy, List<string>? allowedFields)
    {
        if (string.IsNullOrEmpty(orderBy)) return null;
        if (allowedFields == null || allowedFields.Count == 0) return null;

        // Extract field name (remove ASC/DESC suffix if present)
        var field = orderBy.Split(' ')[0];
        return allowedFields.Contains(field, StringComparer.OrdinalIgnoreCase) ? orderBy : null;
    }

    private static Dictionary<string, object?> MergeParameters(object routeParams, Dictionary<string, object?>? bodyParams)
    {
        var result = new Dictionary<string, object?>(bodyParams ?? new Dictionary<string, object?>());

        if (routeParams is Dictionary<string, object?> routeDict)
        {
            foreach (var (key, value) in routeDict)
            {
                result[key] = value;
            }
        }

        return result;
    }
}

/// <summary>
/// Simple result type for operations that can fail.
/// </summary>
internal class OperationResult<T>
{
    public T? Value { get; }
    public ApiError? Error { get; }
    public ValidationProblemDetails? ValidationErrors { get; }
    public bool IsSuccess => Error == null && ValidationErrors == null;
    public bool IsFailure => !IsSuccess;
    public bool HasValidationErrors => ValidationErrors != null;

    private OperationResult(T? value, ApiError? error, ValidationProblemDetails? validationErrors = null)
    {
        Value = value;
        Error = error;
        ValidationErrors = validationErrors;
    }

    public static OperationResult<T> Success(T value) => new(value, null);
    public static OperationResult<T> Failure(ApiError error) => new(default, error);
    public static OperationResult<T> ValidationFailure(ValidationProblemDetails errors) => new(default, null, errors);
}
