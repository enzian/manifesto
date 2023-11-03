using System.Text.Json;
using System.Text.Json.Serialization;

namespace Manifesto.AspNet.Controllers;

public record struct Metadata
{
    public string Name { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Labels { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Annotations { get; init; }
    public string? Revision { get; init; }
}

public record struct Manifest
{
    public string? Kind { get; init; }
    public string? Group { get; init; }
    public string? Version { get; init; }
    public Metadata Metadata { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> Subdocuments { get; set; }
}

public class WatchEvent
{
    public string Type { get; set; }
    public Manifest Object { get; set; }
}
