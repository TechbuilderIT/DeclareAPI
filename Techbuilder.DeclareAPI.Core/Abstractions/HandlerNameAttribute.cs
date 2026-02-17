namespace Techbuilder.DeclareAPI.Core.Abstractions;

/// <summary>
/// Specifies the name used to reference this handler in YAML configuration.
/// If not specified, the class name (without "Handler" suffix) is used.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HandlerNameAttribute : Attribute
{
    public string Name { get; }

    public HandlerNameAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Handler name cannot be null or empty.", nameof(name));
        
        Name = name;
    }
}
