using YamlDotNet.Serialization;

namespace Techbuilder.DeclareAPI.Core.Configuration;

/// <summary>
/// Configuration for an API entity (e.g., Patient, Appointment).
/// </summary>
public class EntityConfig
{
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "endpoints")]
    public Dictionary<string, EndpointConfig> Endpoints { get; set; } = new();
}
