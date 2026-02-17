using System.Reflection;
using Techbuilder.DeclareAPI.Core.Abstractions;

namespace Techbuilder.DeclareAPI.Handlers;

/// <summary>
/// Default implementation of IHandlerRegistry that scans assemblies for handlers.
/// </summary>
public class HandlerRegistry : IHandlerRegistry
{
    private readonly Dictionary<string, HandlerMetadata> _handlers;

    public HandlerRegistry(IEnumerable<Assembly> assemblies)
    {
        _handlers = new Dictionary<string, HandlerMetadata>(StringComparer.OrdinalIgnoreCase);
        ScanAssemblies(assemblies);
    }

    private void ScanAssemblies(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICustomHandler<,>)));

            foreach (var handlerType in handlerTypes)
            {
                RegisterHandler(handlerType);
            }
        }
    }

    private void RegisterHandler(Type handlerType)
    {
        var handlerInterface = handlerType.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICustomHandler<,>));

        var genericArgs = handlerInterface.GetGenericArguments();
        var requestType = genericArgs[0];
        var responseType = genericArgs[1];

        var name = GetHandlerName(handlerType);

        var metadata = new HandlerMetadata
        {
            Name = name,
            HandlerType = handlerType,
            RequestType = requestType,
            ResponseType = responseType
        };

        if (_handlers.ContainsKey(name))
        {
            throw new InvalidOperationException(
                $"Duplicate handler name '{name}'. Handler names must be unique. " +
                $"Conflicting types: {_handlers[name].HandlerType.FullName} and {handlerType.FullName}");
        }

        _handlers[name] = metadata;
    }

    private static string GetHandlerName(Type handlerType)
    {
        // Check for HandlerNameAttribute
        var nameAttribute = handlerType.GetCustomAttribute<HandlerNameAttribute>();
        if (nameAttribute != null)
        {
            return nameAttribute.Name;
        }

        // Default: use class name without "Handler" suffix
        var name = handlerType.Name;
        if (name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^7];
        }

        return name;
    }

    public HandlerMetadata? GetHandler(string handlerName)
    {
        return _handlers.TryGetValue(handlerName, out var metadata) ? metadata : null;
    }

    public IReadOnlyCollection<HandlerMetadata> GetAllHandlers()
    {
        return _handlers.Values.ToList().AsReadOnly();
    }

    public bool HasHandler(string handlerName)
    {
        return _handlers.ContainsKey(handlerName);
    }
}
