using System.Text.Json.Serialization;

namespace Logistics.Web.Services;

public sealed class AtiLoadType
{
    [JsonPropertyName("dictionary_item_id")]
    public int Id { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new();

    public string Name => Attributes.TryGetValue("name", out var n) ? n.Trim() : "";
}