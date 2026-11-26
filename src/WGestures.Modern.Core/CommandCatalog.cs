using System.Text.Json.Nodes;

namespace WGestures.Modern.Core;

public sealed record CommandField(string Key, string Label, string Kind, string DefaultJson, string? Help = null);
public sealed record CommandDefinition(string Id, string Label, IReadOnlyList<CommandField> Fields);

public static class CommandCatalog
{
    private const string Prefix = "WGestures.Core.Commands.Impl.";
    private const string Suffix = ", WGestures.Core";
    private static CommandField F(string key, string label, string kind, string value, string? help = null)
        => new(key, label, kind, value, help);
    public static IReadOnlyList<CommandDefinition> Definitions { get; } = new CommandDefinition[]
    {
        new("HotKey", "Keyboard shortcut", new[] {
            F("Modifiers","Modifiers (virtual-key codes)","keys","[]","Ctrl=162, Shift=160, Alt=164, Win=91"),
            F("Keys","Keys (virtual-key codes)","keys","[67]","A=65, C=67, V=86, Enter=13, Escape=27, F1=112") }),
        new("WindowControl", "Window operation", new[] { F("ChangeWindowStateTo","Operation","window","0") }),
        new("OpenFile", "Open file or application", new[] { F("FilePath","File or application path","text","\"\"") }),
        new("GotoUrl", "Open website", new[] { F("Url","URL","text","\"https://\"") }),
        new("WebSearch", "Search selected text", new[] {
            F("SearchEngineUrl","Search URL ({0} is query)","text","\"https://www.google.com/search?q={0}\""),
            F("SearchEngingName","Search engine name","text","\"Google\""),
            F("UseBrowser","Custom browser path (optional)","nullable","null") }),
        new("SendText", "Send text / keys", new[] { F("Text","Text / SendKeys expression","multiline","\"\"") }),
        new("Cmd", "Command line", new[] {
            F("Code","Command script","multiline","\"\""), F("ShowWindow","Show command window","bool","true"),
            F("AutoSetWorkingDir","Use target app working directory","bool","true") }),
        new("Script", "Lua script", new[] {
            F("InitScript","Initialization","multiline","\"\""), F("Script","Execution","multiline","\"\""),
            F("HandleModifiers","Observe modifiers","bool","false"), F("GestureRecognizedScript","Recognized callback","multiline","\"\""),
            F("ModifierTriggeredScript","Modifier callback","multiline","\"\""), F("GestureEndedScript","Ended callback","multiline","\"\"") }),
        new("ChangeAudioVolume","Change volume",new[] { F("Delta","Volume step","int","1") }),
        new("TaskSwitcher","Task switcher",Array.Empty<CommandField>()),
        new("PauseWGestures","Pause / resume",Array.Empty<CommandField>()),
        new("DoNothing","Do nothing",Array.Empty<CommandField>())
    };
    public static CommandDefinition? Find(string id) => Definitions.FirstOrDefault(d => d.Id == id);
    public static string? Identify(JsonNode? node)
    {
        if (node is not JsonObject command) return null;
        var name = command["$type"]?.GetValue<string>();
        return Definitions.FirstOrDefault(d => name == Prefix + d.Id + "Command" + Suffix)?.Id;
    }

    public static JsonObject Create(string id)
    {
        var def = Find(id) ?? throw new ArgumentException("Unsupported command.");
        var result = new JsonObject { ["$type"] = Prefix + id + "Command" + Suffix };
        foreach (var field in def.Fields) result[field.Key] = JsonNode.Parse(field.DefaultJson);
        return result;
    }

    // Explicit fields only: unknown metadata is not forwarded to executable command objects.
    public static JsonObject Normalize(JsonNode? raw)
    {
        if (raw is null) return Create("DoNothing");
        var id = Identify(raw) ?? throw new InvalidDataException("Unknown or unsupported command type.");
        var def = Find(id)!;
        var result = Create(id);
        var input = (JsonObject)raw;
        foreach (var field in def.Fields)
        {
            if (!input.ContainsKey(field.Key)) continue;
            var node = input[field.Key];
            try
            {
                if (field.Kind == "keys")
                {
                    if (node is not JsonArray keys || keys.Count > 32 ||
                        keys.Any(k => k is null || k.GetValue<int>() is < 1 or > 254))
                        throw new InvalidDataException("Invalid virtual-key list.");
                }
                else if (field.Kind == "bool") _ = node!.GetValue<bool>();
                else if (field.Kind == "window")
                {
                    if (node!.GetValue<int>() is < 0 or > 5) throw new InvalidDataException("Invalid window operation.");
                }
                else if (field.Kind == "int")
                {
                    if (node!.GetValue<int>() is < -100 or > 100) throw new InvalidDataException("Invalid volume step.");
                }
                else
                {
                    if (node is not null && node.GetValue<string>().Length > 65536)
                        throw new InvalidDataException("Command text is too long.");
                    if (node is null && field.Kind != "nullable" && field.Kind != "multiline")
                        throw new InvalidDataException("Missing command text.");
                }
                result[field.Key] = node?.DeepClone();
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException or NullReferenceException)
            {
                throw new InvalidDataException("Invalid command field: " + field.Key, e);
            }
        }
        return result;
    }
}
