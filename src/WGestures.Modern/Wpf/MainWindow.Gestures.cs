using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using WGestures.Modern.Core;
using Path = System.IO.Path;

namespace WGestures.Modern;

internal sealed partial class MainWindow
{
    private sealed record Option(int Value, string Label);
    private static readonly Option[] Triggers =
    {
        new(1,"Right button"), new(2,"Middle button"), new(4,"X1"), new(8,"X2")
    };
    private static readonly Option[] Modifiers =
    {
        new(0,"None"), new(1,"Wheel forward"), new(2,"Wheel backward"), new(4,"Middle click"),
        new(8,"Left click"), new(16,"Right click"), new(32,"X1"), new(64,"X2")
    };
    private static readonly string[] WindowOperations =
    {
        "Maximize / restore","Minimize","Close","Always on top","Dock left","Dock right"
    };

    private static ComboBox OptionBox(Option[] options, int value)
    {
        var combo = new ComboBox { ItemsSource = options, DisplayMemberPath = nameof(Option.Label),
            SelectedValuePath = nameof(Option.Value), MinHeight = 34 };
        combo.SelectedValue = value;
        return combo;
    }
    private static int ReadOption(ComboBox box) => box.SelectedValue is int value
        ? value : throw new ArgumentException("Choose a valid option.");

    private static DataGrid GridForRows(params (string Header, string Path, double Weight)[] columns)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, MinHeight = 200,
            RowHeight = 42, ColumnHeaderHeight = 36, Height = 400
        };
        foreach (var col in columns)
            grid.Columns.Add(new DataGridTextColumn { Header = col.Header, Binding = new Binding(col.Path),
                Width = new DataGridLength(col.Weight, DataGridLengthUnitType.Star), MinWidth = 52 });
        return grid;
    }

    private void RenderGestures()
    {
        var workspace = _workspace!;
        var profiles = workspace.Profiles;
        var profileChoice = new ComboBox
        {
            ItemsSource = profiles, DisplayMemberPath = nameof(GestureProfile.Name), SelectedValuePath = nameof(GestureProfile.Key),
            MinHeight = 34, SelectedValue = _profile
        };
        if (profileChoice.SelectedIndex < 0) profileChoice.SelectedIndex = 0;
        _profile = (string)profileChoice.SelectedValue;
        var search = Input(_query);
        var resultCount = Text("", 12, muted: true);
        var list = GridForRows(("Gesture", nameof(GestureRow.Name), 1.7),
            ("Path", nameof(GestureRow.Path), .8), ("Button", nameof(GestureRow.TriggerLabel), .8), ("Action", nameof(GestureRow.Command), 1.0));
        AutomationProperties.SetName(list, "Gesture library");
        void Refresh(int? select = null)
        {
            var profile = workspace.Profiles.First(p => p.Key == _profile);
            var rows = profile.Gestures.Where(r => string.IsNullOrWhiteSpace(_query) ||
                (r.Name + " " + r.Summary + " " + r.ModifierLabel).Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
            list.ItemsSource = rows;
            resultCount.Text = $"{rows.Length} of {profile.Gestures.Count} gestures";
            list.SelectedItem = rows.FirstOrDefault(r => r.Index == select) ?? rows.FirstOrDefault();
        }
        GestureRow? Selected() => list.SelectedItem as GestureRow;
        void EditSelected()
        {
            if (Selected() is { } row && EditGestureDialog(row) is int i) Refresh(i);
        }
        profileChoice.SelectionChanged += (_, _) =>
        {
            if (profileChoice.SelectedValue is string key) { _profile = key; Refresh(); }
        };
        search.TextChanged += (_, _) => { _query = search.Text; Refresh(); };
        list.MouseDoubleClick += (_, _) => EditSelected();
        var toolbar = Actions(Button("New gesture", () => { if (EditGestureDialog(null) is int i) { _query = ""; search.Text = ""; Refresh(i); } }, true),
            Button("Edit", EditSelected),
            Button("Delete", () =>
            {
                if (Selected() is not { } row || !Confirm("Delete gesture", $"Remove {row.Name}?")) return;
                workspace.DeleteGesture(row.ProfileKey, row.Index); Changed(); Refresh();
            }),
            Button("Move up", () => { if (Selected() is { } row) { workspace.MoveGesture(row.ProfileKey, row.Index, -1); Changed(); Refresh(row.Index); } }),
            Button("Move down", () => { if (Selected() is { } row) { workspace.MoveGesture(row.ProfileKey, row.Index, 1); Changed(); Refresh(row.Index); } }));
        Add(_page, Card("Gesture library", Field("Application profile", profileChoice),
            Field("Search", search, "Search by name, direction, trigger or command."), toolbar, resultCount, list));
        Add(_page, Card("Editing gestures", Text("Double-click a row to open its editor. Draw a path, select a mouse button and modifier, then configure the action. Save or apply changes from the bottom bar.", muted: true)));
        Refresh();
    }

    private sealed record EditorForm(StackPanel View, Func<int> Save);
    private sealed record CommandEdit(StackPanel View, Func<JsonObject> Read);

    private EditorForm BuildGestureForm(GestureRow? row)
    {
        var body = new StackPanel();
        var name = Input(row?.Name ?? "", max: 120);
        var trigger = OptionBox(Triggers, row?.Trigger ?? 1);
        var modifier = OptionBox(Modifiers, row?.Modifier ?? 0);
        var execute = Check("Execute immediately when modifier is triggered",
            row is not null && _workspace!.GetExecuteOnModifier(row.ProfileKey, row.Index));
        var path = Input(row?.Path ?? "", max: 32);
        Add(body, Field("Gesture name", name));
        Add(body, Field("Mouse button", trigger));
        Add(body, Field("Modifier", modifier));
        Add(body, execute);
        Add(body, Field("Directions", path, "Draw in the pad below, or add arrows manually."));
        var keys = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var arrow in new[] { "↖", "↑", "↗", "←", "⌫", "→", "↙", "↓", "↘" })
        {
            var button = MakeButton(arrow);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Margin = new Thickness(2);
            AutomationProperties.SetName(button, arrow == "⌫" ? "Remove last direction" : "Append " + arrow);
            button.Click += (_, _) =>
            {
                if (arrow == "⌫") { if (path.Text.Length > 0) path.Text = path.Text[..^1]; }
                else if (path.Text.Length < 32) path.Text += arrow;
            };
            keys.Children.Add(button);
        }
        Add(body, keys);
        Add(body, DrawingPad(path));
        var command = BuildCommandEditor(row is null ? CommandCatalog.Create("HotKey") : _workspace!.GetCommand(row.ProfileKey, row.Index));
        Add(body, Card("Action", command.View));
        return new EditorForm(body, () =>
        {
            int index = _workspace!.UpsertGesture(row?.ProfileKey ?? _profile, row?.Index, name.Text, path.Text,
                ReadOption(trigger), ReadOption(modifier), execute.IsChecked == true, command.Read());
            Changed(); return index;
        });
    }

    private int? EditGestureDialog(GestureRow? row)
    {
        var form = BuildGestureForm(row);
        int? result = null;
        if (EditDialog(row is null ? "New gesture" : "Edit gesture", form.View, row is null ? "Create" : "Save",
            () => { result = form.Save(); }))
        {
            Notify("Gesture saved", "Apply changes to reload the running engine.");
            return result;
        }
        return null;
    }

    private bool EditDialog(string title, UIElement body, string saveLabel, Action save)
    {
        var dialog = new Window
        {
            Owner = this, Title = title, Width = Math.Min(640, ActualWidth), Height = Math.Min(740, ActualHeight),
            MinWidth = 400, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, FontFamily = FontFamily, FontSize = FontSize
        };
        dialog.SetResourceReference(BackgroundProperty, "ShellBackground");
        dialog.SetResourceReference(ForegroundProperty, "MainText");
        var dock = new DockPanel { Margin = new Thickness(22) };
        var heading = Text(title, 24, true); heading.Margin = new Thickness(0, 0, 0, 15);
        DockPanel.SetDock(heading, Dock.Top); dock.Children.Add(heading);
        var error = Text("", 12, true);
        error.Margin = new Thickness(0, 0, 0, 8);
        var yes = MakeButton(saveLabel, true);
        var no = MakeButton("Cancel");
        no.IsCancel = true;
        yes.Click += (_, _) =>
        {
            try { save(); dialog.DialogResult = true; }
            catch (Exception e) { error.Text = e.Message; Program.Log("Editor validation: " + e.Message); }
        };
        var footer = Stack(error, Actions(yes, no));
        DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        dialog.Content = dock;
        return dialog.ShowDialog() == true;
    }

    private CommandEdit BuildCommandEditor(JsonObject existing)
    {
        var body = new StackPanel();
        var combo = new ComboBox { ItemsSource = CommandCatalog.Definitions, DisplayMemberPath = nameof(CommandDefinition.Label),
            SelectedValuePath = nameof(CommandDefinition.Id), MinHeight = 34 };
        var fields = new StackPanel();
        Add(body, Field("Command", combo)); Add(body, fields);
        var readers = new Dictionary<string, Func<JsonNode?>>();
        var keyFields = new Dictionary<string, TextBox>();
        void RenderFields(JsonObject raw)
        {
            fields.Children.Clear(); readers.Clear(); keyFields.Clear();
            if (combo.SelectedValue is not string id)
            {
                Add(fields, Text("This imported action is not recognized. Choose a supported command.", muted: true));
                return;
            }
            var def = CommandCatalog.Find(id)!;
            if (id is "Script" or "Cmd") Add(fields, Text("This action executes code. Enable script actions explicitly in Preferences.", muted: true));
            foreach (var field in def.Fields)
            {
                var value = raw.ContainsKey(field.Key) ? raw[field.Key] : JsonNode.Parse(field.DefaultJson);
                if (field.Kind == "bool")
                {
                    var check = Check(field.Label, value?.GetValue<bool>() ?? false);
                    readers[field.Key] = () => JsonValue.Create(check.IsChecked == true);
                    Add(fields, check);
                }
                else if (field.Kind == "window")
                {
                    var choose = new ComboBox { ItemsSource = WindowOperations, MinHeight = 34,
                        SelectedIndex = value?.GetValue<int>() ?? 0 };
                    readers[field.Key] = () => JsonValue.Create(choose.SelectedIndex);
                    Add(fields, Field(field.Label, choose));
                }
                else
                {
                    string initial = field.Kind == "keys" && value is JsonArray initialKeys
                        ? string.Join(", ", initialKeys.Select(v => v?.ToString()))
                        : field.Kind == "int" ? value?.ToString() ?? "1" : value?.GetValue<string>() ?? "";
                    var input = Input(initial, field.Kind == "multiline");
                    if (field.Kind == "keys") keyFields[field.Key] = input;
                    readers[field.Key] = () =>
                    {
                        if (field.Kind == "keys")
                        {
                            var arr = new JsonArray();
                            foreach (var part in input.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!int.TryParse(part, out int key)) throw new ArgumentException("Invalid virtual-key code: " + part);
                                arr.Add(JsonValue.Create(key));
                            }
                            return arr;
                        }
                        if (field.Kind == "int")
                        {
                            if (!int.TryParse(input.Text, out int number)) throw new ArgumentException("Enter a whole number.");
                            return JsonValue.Create(number);
                        }
                        return field.Kind == "nullable" && string.IsNullOrWhiteSpace(input.Text) ? null : JsonValue.Create(input.Text);
                    };
                    if (field.Key is "FilePath" or "UseBrowser")
                    {
                        var browse = MakeButton("Browse…");
                        browse.Click += (_, _) =>
                        {
                            var picker = new OpenFileDialog { Filter = "Files|*.*", CheckFileExists = true };
                            if (picker.ShowDialog(Window.GetWindow(browse) ?? this) == true) input.Text = picker.FileName;
                        };
                        Add(fields, Field(field.Label, Stack(input, Actions(browse)), field.Help));
                    }
                    else Add(fields, Field(field.Label, input, field.Help));
                }
            }
            if (id == "HotKey" && keyFields.TryGetValue("Modifiers", out var mods) && keyFields.TryGetValue("Keys", out var keys))
            {
                var record = Input("Click here and press the shortcut");
                record.IsReadOnly = true;
                record.PreviewKeyDown += (_, e) =>
                {
                    var key = e.Key == Key.System ? e.SystemKey : e.Key;
                    var code = KeyInterop.VirtualKeyFromKey(key);
                    if (code is < 1 or > 254 || code is 16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165)
                    { e.Handled = true; return; }
                    var m = new List<int>(); var labels = new List<string>();
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { m.Add(162); labels.Add("Ctrl"); }
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { m.Add(160); labels.Add("Shift"); }
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) { m.Add(164); labels.Add("Alt"); }
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) { m.Add(91); labels.Add("Win"); }
                    mods.Text = string.Join(", ", m); keys.Text = code.ToString();
                    labels.Add(key.ToString()); record.Text = string.Join(" + ", labels); e.Handled = true;
                };
                Add(fields, Field("Record shortcut", record, "System-reserved shortcuts may need to be entered manually using key codes."));
            }
        }
        combo.SelectedValue = CommandCatalog.Identify(existing);
        RenderFields(existing);
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedValue is string id) RenderFields(CommandCatalog.Create(id));
        };
        return new CommandEdit(body, () =>
        {
            if (combo.SelectedValue is not string id) throw new ArgumentException("Choose a supported command.");
            var command = CommandCatalog.Create(id);
            foreach (var field in readers) command[field.Key] = field.Value();
            return CommandCatalog.Normalize(command);
        });
    }

    private FrameworkElement DrawingPad(TextBox path)
    {
        var canvas = new Canvas { Height = 140, ClipToBounds = true, Focusable = true };
        canvas.SetResourceReference(Panel.BackgroundProperty, "PadBackground");
        AutomationProperties.SetName(canvas, "Draw gesture with left mouse button");
        var trail = new Polyline { StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round };
        trail.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        canvas.Children.Add(trail);
        GestureStroke? stroke = null;
        canvas.MouseLeftButtonDown += (_, e) =>
        {
            var point = e.GetPosition(canvas);
            if (!canvas.CaptureMouse()) return;
            trail.Points.Clear(); trail.Points.Add(point);
            stroke = new GestureStroke(point.X, point.Y, _settings.EightDirections);
            path.Text = ""; e.Handled = true;
        };
        canvas.MouseMove += (_, e) =>
        {
            if (stroke is null || !canvas.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(canvas);
            if (trail.Points.Count < 2000) trail.Points.Add(point);
            if (stroke.Move(point.X, point.Y)) path.Text = stroke.Path;
        };
        canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
            stroke = null; e.Handled = true;
        };
        canvas.LostMouseCapture += (_, _) => stroke = null;
        var border = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = canvas };
        border.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
        return border;
    }

    private void RenderApps()
    {
        var workspace = _workspace!;
        Add(_page, Actions(Button("Add application…", () =>
        {
            var picker = new OpenFileDialog { Filter = "Applications (*.exe)|*.exe", CheckFileExists = true };
            if (picker.ShowDialog(this) != true) return;
            _profile = workspace.AddProfile(picker.FileName, Path.GetFileNameWithoutExtension(picker.FileName));
            Changed(); Render();
        }, true)));
        foreach (var profile in workspace.Profiles)
        {
            string key = profile.Key;
            var name = Input(profile.Name, max: 120); name.IsEnabled = key != GestureWorkspace.GlobalKey;
            var enabled = Check("Enable gestures for this profile", profile.Enabled);
            var inherit = Check("Inherit global gestures", profile.InheritsGlobal); inherit.IsEnabled = key != GestureWorkspace.GlobalKey;
            var buttons = Actions(Button("Save profile", () =>
            {
                workspace.UpdateProfile(key, name.Text, enabled.IsChecked == true, inherit.IsChecked == true);
                Changed(); Render();
            }, true), Button("View gestures", () => { _profile = key; _query = ""; Navigate("gestures"); }));
            if (key != GestureWorkspace.GlobalKey)
            {
                var remove = Button("Remove profile", () =>
                {
                    if (!Confirm("Remove profile", $"Remove {profile.Name} and its custom gestures?")) return;
                    workspace.DeleteProfile(key);
                    if (_profile == key) _profile = GestureWorkspace.GlobalKey;
                    Changed(); Render();
                });
                remove.Margin = new Thickness(0,0,8,6); buttons.Children.Add(remove);
            }
            Add(_page, Card(profile.Name, Text(profile.ExecutablePath ?? "Shared global profile", muted: true),
                Text($"{profile.Gestures.Count} gestures", 12, muted: true),
                Field("Name", name), enabled, inherit, buttons));
        }
    }

    private void RenderCorners()
    {
        var workspace = _workspace!;
        Add(_page, Card("Screen interactions", Text("Hot corners and edge rubbing must be enabled in Preferences. The same command library is available for all eight slots."),
            Button("Open preferences", () => Navigate("settings"))));
        int i = 0;
        foreach (var slot in workspace.Corners)
        {
            int index = i++;
            Add(_page, Card(slot.Name, Text("Current action: " + slot.Command, muted: true),
                Button("Change action", () =>
                {
                    var edit = BuildCommandEditor(workspace.GetCornerCommand(index));
                    if (EditDialog(slot.Name, edit.View, "Save", () => { workspace.SetCornerCommand(index, edit.Read()); Changed(); }))
                        Render();
                })));
        }
    }
}
