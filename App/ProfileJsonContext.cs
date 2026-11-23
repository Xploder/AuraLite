using System.Text.Json.Serialization;

namespace AuraLite;

[JsonSerializable(typeof(Dictionary<string, LightingProfile>))]
[JsonSerializable(typeof(SortedDictionary<string, LightingProfile>))]
[JsonSerializable(typeof(LightingSettings))]
internal sealed partial class ProfileJsonContext : JsonSerializerContext;
