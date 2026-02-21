using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradePilot.Shared.Serialization;

public static class TradePilotJson
{
    public static readonly JsonSerializerOptions Default = CreateDefault();

    public static JsonSerializerOptions CreateDefault()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    public static JsonSerializerOptions CopyDefault() => new(Default);
}
