using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WGestures.Modern.Core;

public sealed record GestureRow(string ProfileKey, int Index, string Name, int Trigger, string Path,
    int Modifier, string Command, int Order)
{
    public string TriggerLabel => Trigger switch
    {
        1 => "Right", 2 => "Middle", 4 => "X1", 8 => "X2", _ => $"Button {Trigger}"
    };
    public string ModifierLabel => Modifier switch
    {
        0 => "None", 1 => "Wheel forward", 2 => "Wheel backward", 4 => "Middle click",
        8 => "Left click", 16 => "Right click", 32 => "X1", 64 => "X2", _ => $"Modifier {Modifier}"
    };
    public string Summary => $"{TriggerLabel}  ·  {(Path.Length == 0 ? "—" : Path)}  ·  {Command}";
}

public sealed record GestureProfile(string Key, string Name, string? ExecutablePath, bool Enabled,
    bool InheritsGlobal, IReadOnlyList<GestureRow> Gestures);
public sealed record CornerAction(string Name, string Command);

/// <summary>
/// A version-3 WG1 document editor. CLR type annotations are opaque JSON strings.
/// No command is instantiated and no legacy file is ever written by this model.
/// </summary>
public sealed class GestureWorkspace
{
    public const string GlobalKey = "";
    private const int MaxBytes = 16 * 1024 * 1024;
    private const string Arrows = "↑↗→↘↓↙←↖";
    private readonly JsonObject _document;

    private GestureWorkspace(JsonObject document)
    {
        _document = document;
        _ = Profiles;
        _ = Corners;
    }

    public static GestureWorkspace Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new InvalidDataException("Configuration exceeds 16 MiB.");
        var document = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject
            ?? throw new InvalidDataException("Expected a JSON object.");
        if (document["FileVersion"]?.GetValue<string>() != "3")
            throw new InvalidDataException("Only original WGestures gesture format version 3 is supported.");
        return new GestureWorkspace(document);
    }

    public static GestureWorkspace Open(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Configuration exceeds 16 MiB.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return Parse(reader.ReadToEnd());
    }

    private JsonObject App(string key) => key == GlobalKey
        ? Object(_document["Global"], "Global")
        : Object(Object(_document["Apps"], "Apps")[key], "Application");

    private static JsonObject Object(JsonNode? node, string field) =>
        node as JsonObject ?? throw new InvalidDataException($"Invalid or missing {field}.");
    private static JsonArray Array(JsonNode? node, string field) =>
        node as JsonArray ?? throw new InvalidDataException($"Invalid or missing {field}.");
    private static JsonArray Intents(JsonObject profile) => Array(profile["GestureIntents"], "GestureIntents");

    public IReadOnlyList<GestureProfile> Profiles
    {
        get
        {
            var result = new List<GestureProfile> { ReadProfile(GlobalKey, App(GlobalKey)) };
            foreach (var item in Object(_document["Apps"], "Apps"))
            {
                if (string.IsNullOrEmpty(item.Key)) throw new InvalidDataException("Empty application key.");
                result.Add(ReadProfile(item.Key, Object(item.Value, "Application")));
            }
            return result;
        }
    }

    private static GestureProfile ReadProfile(string key, JsonObject app)
    {
        var rows = Intents(app).Select((node, index) =>
        {
            var intent = Object(node, "Intent");
            var gesture = Object(intent["Gesture"], "Gesture");
            var dirs = Array(gesture["Dirs"], "Dirs").Select(d =>
            {
                int direction = d?.GetValue<int>() ?? -1;
                if (direction is < 0 or > 7) throw new InvalidDataException("Unknown gesture direction.");
                return Arrows[direction];
            });
            return new GestureRow(key, index, intent["Name"]?.GetValue<string>() ?? "(Unnamed)",
                gesture["GestureButton"]?.GetValue<int>() ?? throw new InvalidDataException("Missing trigger."),
                new string(dirs.ToArray()),
                gesture["Modifier"]?.GetValue<int>() ?? 0, CommandName(intent["Command"]),
                intent["Order"]?.GetValue<int>() ?? index);
        }).OrderBy(row => row.Order).ThenBy(row => row.Index).ToArray();
        return new GestureProfile(key, key == GlobalKey ? "Global" : app["Name"]?.GetValue<string>() ?? key,
            key == GlobalKey ? null : app["ExecutablePath"]?.GetValue<string>() ?? key,
            app["IsGesturingEnabled"]?.GetValue<bool>() ?? true,
            key != GlobalKey && (app["InheritGlobalGestures"]?.GetValue<bool>() ?? false), rows);
    }

    private static string CommandName(JsonNode? node)
    {
        if (node is null) return "Unassigned";
        var command = Object(node, "Command");
        var type = command["$type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type)) return "Unknown command";
        // Display only; never resolve or instantiate a CLR type from imported metadata.
        var name = type.Split(',')[0].Trim().Split('.').Last();
        return name.EndsWith("Command", StringComparison.Ordinal) ? name[..^7] : name;
    }

    public IReadOnlyList<CornerAction> Corners
    {
        get
        {
            // Original radio button tags define the persisted order.
            var items = Array(_document["HotCornerCommands"], "HotCornerCommands");
            if (items.Count != 8) throw new InvalidDataException("Expected eight corner/edge slots.");
            var names = new[] { "Bottom-left corner", "Top-left corner", "Top-right corner", "Bottom-right corner", "Left edge", "Top edge", "Right edge", "Bottom edge" };
            return items.Select((item, i) => new CornerAction(names[i],
                CommandName(item))).ToArray();
        }
    }

    public void EditGesture(string profileKey, int index, string name, string path)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 120) throw new ArgumentException("Name must contain 1–120 characters.");
        if (path.Length > 32 || path.Any(c => !Arrows.Contains(c)))
            throw new ArgumentException("Use up to 32 direction arrows.");
        var intents = Intents(App(profileKey));
        var intent = Object(intents[index], "Intent");
        var gesture = Object(intent["Gesture"], "Gesture");
        int trigger = gesture["GestureButton"]!.GetValue<int>();
        int modifier = gesture["Modifier"]?.GetValue<int>() ?? 0;
        if (path.Length == 0 && modifier == 0) throw new ArgumentException("A gesture needs a direction or modifier.");
        if (ReadProfile(profileKey, App(profileKey)).Gestures.Any(row =>
            row.Index != index && row.Trigger == trigger && row.Modifier == modifier && row.Path == path))
            throw new ArgumentException("A gesture with this trigger, path and modifier already exists in this profile.");
        var directions = new JsonArray();
        foreach (char c in path) directions.Add(JsonValue.Create(Arrows.IndexOf(c)));
        gesture["Dirs"] = directions;
        intent["Name"] = name;
    }

    public JsonObject GetCommand(string profileKey, int index)
    {
        var node = Object(Intents(App(profileKey))[index], "Intent")["Command"];
        return node is JsonObject obj ? (JsonObject)obj.DeepClone() : CommandCatalog.Create("DoNothing");
    }

    public bool GetExecuteOnModifier(string profileKey, int index) =>
        Object(Intents(App(profileKey))[index], "Intent")["ExecuteOnModifier"]?.GetValue<bool>() ?? false;

    private static JsonObject NewIntent(string name, string path, int trigger, int modifier,
        bool executeOnModifier, JsonObject command)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 120) throw new ArgumentException("Name must contain 1–120 characters.");
        if (trigger is not (1 or 2 or 4 or 8)) throw new ArgumentException("Choose a mouse button.");
        if (modifier is not (0 or 1 or 2 or 4 or 8 or 16 or 32 or 64))
            throw new ArgumentException("Choose a supported modifier.");
        if (path.Length > 32 || path.Any(c => !Arrows.Contains(c)) || (path.Length == 0 && modifier == 0))
            throw new ArgumentException("A gesture needs a direction or modifier; use at most 32 arrows.");
        if (executeOnModifier && modifier == 0)
            throw new ArgumentException("Execution on modifier requires a modifier.");
        var dirs = new JsonArray();
        foreach (char c in path) dirs.Add(JsonValue.Create(Arrows.IndexOf(c)));
        return new JsonObject
        {
            ["Name"] = name,
            ["Gesture"] = new JsonObject { ["GestureButton"] = trigger, ["Modifier"] = modifier, ["Dirs"] = dirs },
            ["ExecuteOnModifier"] = executeOnModifier,
            ["Command"] = CommandCatalog.Normalize(command)
        };
    }

    public int UpsertGesture(string key, int? index, string name, string path, int trigger, int modifier,
        bool executeOnModifier, JsonObject command)
    {
        var entry = NewIntent(name, path, trigger, modifier, executeOnModifier, command);
        var profile = App(key);
        var intents = Intents(profile);
        var rows = ReadProfile(key, profile).Gestures;
        if (rows.Any(r => r.Index != index && r.Trigger == trigger && r.Modifier == modifier && r.Path == path))
            throw new ArgumentException("This trigger, path and modifier already exist in the profile.");
        if (index is int i)
        {
            var current = Object(intents[i], "Intent");
            foreach (var property in entry)
                current[property.Key] = property.Value?.DeepClone();
            return i;
        }
        entry["$type"] = "WGestures.App.Gui.Model.OrderableIntent, WGestures";
        entry["Order"] = rows.Count == 0 ? 0 : rows.Max(r => r.Order) + 1;
        intents.Add(entry);
        return intents.Count - 1;
    }

    public void DeleteGesture(string key, int index) => Intents(App(key)).RemoveAt(index);

    public void MoveGesture(string key, int index, int offset)
    {
        var app = App(key);
        var rows = ReadProfile(key, app).Gestures.ToList();
        var position = rows.FindIndex(r => r.Index == index);
        var next = position + offset;
        if (position < 0 || next < 0 || next >= rows.Count) return;
        (rows[position], rows[next]) = (rows[next], rows[position]);
        var intents = Intents(app);
        for (int i = 0; i < rows.Count; i++) Object(intents[rows[i].Index], "Intent")["Order"] = i;
    }

    public void UpdateProfile(string key, string name, bool enabled, bool inherits)
    {
        var app = App(key);
        name = name.Trim();
        if (name.Length is < 1 or > 120) throw new ArgumentException("Enter an application name.");
        if (key != GlobalKey)
        {
            app["Name"] = name;
            app["InheritGlobalGestures"] = inherits;
        }
        app["IsGesturingEnabled"] = enabled;
    }

    public string AddProfile(string path, string name)
    {
        path = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select an executable file (.exe).");
        name = name.Trim();
        if (name.Length is < 1 or > 120) throw new ArgumentException("Enter an application name.");
        string key = path.ToLowerInvariant();
        var apps = Object(_document["Apps"], "Apps");
        if (apps.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Application profile already exists.");
        apps[key] = new JsonObject
        {
            ["$type"] = "WGestures.App.Gui.Model.OrderableExeApp, WGestures",
            ["Order"] = apps.Count + 1,
            ["Name"] = name, ["ExecutablePath"] = key,
            ["InheritGlobalGestures"] = true, ["IsGesturingEnabled"] = true, ["GestureIntents"] = new JsonArray()
        };
        return key;
    }

    public void DeleteProfile(string key)
    {
        if (key == GlobalKey) throw new ArgumentException("The global profile cannot be deleted.");
        Object(_document["Apps"], "Apps").Remove(key);
    }

    public JsonObject GetCornerCommand(int index)
    {
        var node = Array(_document["HotCornerCommands"], "HotCornerCommands")[index];
        return node is JsonObject command ? (JsonObject)command.DeepClone() : CommandCatalog.Create("DoNothing");
    }

    public void SetCornerCommand(int index, JsonObject command)
        => Array(_document["HotCornerCommands"], "HotCornerCommands")[index] = CommandCatalog.Normalize(command);

    public void ValidateForEngine()
    {
        foreach (var profile in Profiles)
        {
            var seen = new HashSet<(int, string, int)>();
            foreach (var row in profile.Gestures)
            {
                if (!seen.Add((row.Trigger, row.Path, row.Modifier)))
                    throw new InvalidDataException("Duplicate gesture in profile " + profile.Name);
                _ = NewIntent(row.Name, row.Path, row.Trigger, row.Modifier,
                    GetExecuteOnModifier(profile.Key, row.Index), GetCommand(profile.Key, row.Index));
            }
        }
        for (int i = 0; i < 8; i++) _ = CommandCatalog.Normalize(GetCornerCommand(i));
    }

    public void Export(string path) => SaveDraft(path);

    public string Serialize() => _document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public void SaveDraft(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, Serialize(), new UTF8Encoding(false));
            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
