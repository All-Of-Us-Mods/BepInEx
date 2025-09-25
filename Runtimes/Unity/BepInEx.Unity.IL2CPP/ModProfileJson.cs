using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BepInEx.Unity.IL2CPP;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ModProfileJson))]
public partial class ModProfileJsonContext : JsonSerializerContext;

public struct ModProfileJson()
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string author { get; set; } = "";
    public string description { get; set; } = "";
    public string thumbnail { get; set; } = "";
    public Dictionary<string, string> mods { get; set; } = new();
    public List<string> disabledMods { get; set; } = new();
}
