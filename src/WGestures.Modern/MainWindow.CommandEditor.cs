using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using WGestures.Modern.Core;

namespace WGestures.Modern;

public sealed partial class MainWindow
{
    private sealed record CommandEdit(StackPanel View, Func<JsonObject> Read);
    private CommandEdit CreateCommandEditor(JsonObject existing)
    {
        var body = new StackPanel { Spacing = 10 };
        var kind = new ComboBox { Header = "Action", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var def in CommandCatalog.Definitions)
            kind.Items.Add(new ComboBoxItem { Tag = def.Id, Content = def.Label });
        var fields = new StackPanel { Spacing = 10 };
        body.Children.Add(kind);
        body.Children.Add(fields);
        var id = CommandCatalog.Identify(existing);
        var known = CommandCatalog.Definitions.ToList().FindIndex(d => d.Id == id);
        if (known < 0) fields.Children.Add(Paragraph("Unsupported imported command. Select a known action to replace it."));
        var readers = new Dictionary<string, Func<JsonNode?>>();
        var keyFields = new Dictionary<string, TextBox>();
        void RenderFields(JsonObject raw)
        {
            fields.Children.Clear();
            readers.Clear();
            keyFields.Clear();
            if (kind.SelectedItem is not ComboBoxItem { Tag: string type }) return;
            var def = CommandCatalog.Find(type)!;
            if (type is "Script" or "Cmd")
                fields.Children.Add(Paragraph("This action can execute code. It runs only if enabled in Preferences."));
            foreach (var field in def.Fields)
            {
                var value = raw.ContainsKey(field.Key) ? raw[field.Key] : JsonNode.Parse(field.DefaultJson);
                if (field.Kind == "bool")
                {
                    var toggle = new ToggleSwitch { Header = field.Label, IsOn = value?.GetValue<bool>() ?? false };
                    readers[field.Key] = () => JsonValue.Create(toggle.IsOn);
                    fields.Children.Add(toggle);
                }
                else if (field.Kind == "window")
                {
                    var combo = new ComboBox { Header = field.Label, HorizontalAlignment = HorizontalAlignment.Stretch };
                    foreach (var label in new[] { "Maximize / restore", "Minimize", "Close", "Always on top", "Dock left", "Dock right" })
                        combo.Items.Add(label);
                    combo.SelectedIndex = value?.GetValue<int>() ?? 0;
                    readers[field.Key] = () => JsonValue.Create(combo.SelectedIndex);
                    fields.Children.Add(combo);
                }
                else
                {
                    var multiline = field.Kind == "multiline";
                    var text = new TextBox
                    {
                        Header = field.Label, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                        MinHeight = multiline ? 90 : 34,
                        Text = field.Kind == "keys" && value is JsonArray keys
                            ? string.Join(", ", keys.Select(k => k?.ToString()))
                            : field.Kind == "int" ? value?.ToString() ?? "1" : value?.GetValue<string>() ?? ""
                    };
                    readers[field.Key] = () =>
                    {
                        if (field.Kind == "keys")
                        {
                            var array = new JsonArray();
                            foreach (string part in text.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!int.TryParse(part, out int key)) throw new ArgumentException("Invalid virtual-key code: " + part);
                                array.Add(JsonValue.Create(key));
                            }
                            return array;
                        }
                        if (field.Kind == "int")
                        {
                            if (!int.TryParse(text.Text, out int number)) throw new ArgumentException("Enter a whole number.");
                            return JsonValue.Create(number);
                        }
                        return field.Kind == "nullable" && string.IsNullOrWhiteSpace(text.Text) ? null : JsonValue.Create(text.Text);
                    };
                    fields.Children.Add(text);
                    if (field.Kind == "keys") keyFields[field.Key] = text;
                }
                if (field.Help is not null) fields.Children.Add(Paragraph(field.Help));
            }
            if (type == "HotKey" && keyFields.TryGetValue("Modifiers", out var modifierBox) && keyFields.TryGetValue("Keys", out var keyBox))
            {
                var record = new TextBox { Header = "Record shortcut", PlaceholderText = "Click here, then press a shortcut" };
                record.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) =>
                {
                    e.Handled = true;
                    int key = (int)e.Key;
                    if (key is 16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165) return;
                    var modifiers = new List<int>();
                    bool IsDown(Windows.System.VirtualKey k) =>
                        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(k) &
                         Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
                    if (IsDown(Windows.System.VirtualKey.Control)) modifiers.Add(162);
                    if (IsDown(Windows.System.VirtualKey.Shift)) modifiers.Add(160);
                    if (IsDown(Windows.System.VirtualKey.Menu)) modifiers.Add(164);
                    if (IsDown(Windows.System.VirtualKey.LeftWindows) || IsDown(Windows.System.VirtualKey.RightWindows)) modifiers.Add(91);
                    if (key is < 1 or > 254) return;
                    modifierBox.Text = string.Join(", ", modifiers);
                    keyBox.Text = key.ToString();
                    record.Text = string.Join(" + ", modifiers.Select(m => m switch { 162 => "Ctrl", 160 => "Shift", 164 => "Alt", _ => "Win" }).Append(e.Key.ToString()));
                }), true);
                fields.Children.Add(record);
            }
        }
        kind.SelectedIndex = known;
        if (known >= 0) RenderFields(existing);
        kind.SelectionChanged += (_, _) =>
        {
            if (kind.SelectedItem is ComboBoxItem { Tag: string selected }) RenderFields(CommandCatalog.Create(selected));
        };
        return new CommandEdit(body, () =>
        {
            if (kind.SelectedItem is not ComboBoxItem { Tag: string selected }) throw new ArgumentException("Select a supported action.");
            var command = CommandCatalog.Create(selected);
            foreach (var reader in readers) command[reader.Key] = reader.Value();
            return CommandCatalog.Normalize(command);
        });
    }

    private static ComboBox Choices(string header, (int Value, string Label)[] items, int selected)
    {
        var combo = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items) combo.Items.Add(new ComboBoxItem { Content = item.Label, Tag = item.Value });
        combo.SelectedIndex = Array.FindIndex(items, i => i.Value == selected);
        return combo;
    }

    private static int Choice(ComboBox box) =>
        box.SelectedItem is ComboBoxItem { Tag: int value } ? value : throw new ArgumentException("Select " + box.Header + ".");

    private void ShowGestureEditor(StackPanel host, GestureRow? row, Action<int?> refresh)
    {
        host.Children.Clear();
        host.Children.Add(Heading(row?.Name ?? "New gesture", 20));
        var name = new TextBox { Header = "Gesture name", Text = row?.Name ?? "", MaxLength = 120 };
        var trigger = Choices("Mouse button", new[] { (1,"Right"), (2,"Middle"), (4,"X1"), (8,"X2") }, row?.Trigger ?? 1);
        var modifier = Choices("Modifier", new[] { (0,"None"), (1,"Wheel forward"), (2,"Wheel backward"), (4,"Middle click"), (8,"Left click"), (16,"Right click"), (32,"X1"), (64,"X2") }, row?.Modifier ?? 0);
        var execute = new ToggleSwitch { Header = "Execute when modifier is triggered", IsOn = row is not null && _workspace!.GetExecuteOnModifier(row.ProfileKey, row.Index) };
        var path = new TextBox { Header = "Directions", Text = row?.Path ?? "", MaxLength = 32 };
        foreach (var item in new UIElement[] { name, trigger, modifier, execute, path }) host.Children.Add(item);
        var keys = new[] { "↖","↑","↗","←","⌫","→","↙","↓","↘" };
        var grid = new Grid { ColumnSpacing = 5, RowSpacing = 5 };
        for (int i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (int i = 0; i < keys.Length; i++)
        {
            string arrow = keys[i];
            var button = Action(arrow, () =>
            {
                if (arrow == "⌫") { if (path.Text.Length > 0) path.Text = path.Text[..^1]; }
                else if (path.Text.Length < 32) path.Text += arrow;
            });
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, arrow == "⌫" ? "Remove last direction" : "Append " + arrow);
            Grid.SetRow(button, i / 3); Grid.SetColumn(button, i % 3); grid.Children.Add(button);
        }
        host.Children.Add(grid);
        host.Children.Add(CreateDrawingPad(path));
        var editor = CreateCommandEditor(row is null ? CommandCatalog.Create("HotKey") : _workspace!.GetCommand(row.ProfileKey, row.Index));
        host.Children.Add(editor.View);
        host.Children.Add(Action(row is null ? "Add gesture" : "Save gesture", () =>
        {
            try
            {
                int index = _workspace!.UpsertGesture(_profile, row?.Index, name.Text, path.Text,
                    Choice(trigger), Choice(modifier), execute.IsOn, editor.Read());
                Changed();
                refresh(index);
                Notify("Gesture saved", "Apply the configuration to reload the running engine.", InfoBarSeverity.Success);
            }
            catch (Exception e) { Notify("Invalid gesture", e.Message, InfoBarSeverity.Error); }
        }, true));
        if (row is not null)
        {
            var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            order.Children.Add(Action("Move up", () => { _workspace!.MoveGesture(row.ProfileKey, row.Index, -1); Changed(); refresh(row.Index); }));
            order.Children.Add(Action("Move down", () => { _workspace!.MoveGesture(row.ProfileKey, row.Index, 1); Changed(); refresh(row.Index); }));
            host.Children.Add(order);
            host.Children.Add(AsyncAction("Delete gesture", async () =>
            {
                if (!await ConfirmAsync("Delete gesture", $"Remove {row.Name} from this profile?", "Delete")) return;
                _workspace!.DeleteGesture(row.ProfileKey, row.Index);
                Changed();
                refresh(null);
            }));
        }
    }

    private Border CreateDrawingPad(TextBox path)
    {
        var canvas = new Canvas
        {
            Height = 120, Background = (Brush)Root.Resources["PadBrush"]
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(canvas, "Draw a gesture with the left mouse button or touch");
        var trail = new Polyline { Stroke = (Brush)Root.Resources["PadStroke"], StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round };
        canvas.Children.Add(trail);
        GestureStroke? stroke = null;
        uint? pointer = null;
        canvas.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(canvas);
            if (!point.Properties.IsLeftButtonPressed || pointer is not null) return;
            pointer = e.Pointer.PointerId;
            canvas.CapturePointer(e.Pointer);
            trail.Points.Clear();
            trail.Points.Add(point.Position);
            stroke = new GestureStroke(point.Position.X, point.Position.Y, _settings.EightDirections);
            path.Text = "";
            e.Handled = true;
        };
        canvas.PointerMoved += (_, e) =>
        {
            if (stroke is null || pointer != e.Pointer.PointerId) return;
            var point = e.GetCurrentPoint(canvas).Position;
            if (trail.Points.Count < 2000) trail.Points.Add(point);
            if (stroke.Move(point.X, point.Y)) path.Text = stroke.Path;
            e.Handled = true;
        };
        canvas.PointerReleased += (_, e) =>
        {
            if (pointer != e.Pointer.PointerId) return;
            canvas.ReleasePointerCapture(e.Pointer);
            stroke = null; pointer = null;
            e.Handled = true;
        };
        canvas.PointerCaptureLost += (_, _) => { stroke = null; pointer = null; };
        return Card(Paragraph("Draw the gesture here with the left button. You can also use the direction keys above."), canvas);
    }

    private void Changed()
    {
        _dirty = true;
        UpdateSource();
    }
}
