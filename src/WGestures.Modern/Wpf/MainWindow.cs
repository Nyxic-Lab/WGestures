using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WGestures.Modern.Core;

namespace WGestures.Modern;

internal sealed partial class MainWindow : Window
{
    private static readonly (string Id, string Label, string Icon)[] Pages =
    {
        ("overview", "Overview", "\uE80F"), ("gestures", "Gestures", "\uE104"),
        ("apps", "Applications", "\uE71D"), ("corners", "Corners & edges", "\uE740"),
        ("settings", "Preferences", "\uE713"), ("about", "About", "\uE946")
    };

    private readonly ListBox _nav = new();
    private readonly TextBlock _pageTitle = new(), _description = new(), _sourceText = new(), _engineStatus = new(), _notice = new();
    private readonly StackPanel _page = new();
    private readonly ScrollViewer _scroll = new();
    private readonly DispatcherTimer _statusTimer;
    private GestureWorkspace? _workspace;
    private EngineSettings _settings;
    private string _source = "No configuration loaded";
    private string _selectedPage = "overview", _profile = GestureWorkspace.GlobalKey, _query = "";
    private bool _dirty, _busy;
    private bool _smoke;
    private readonly string _draftPath = EngineSettings.DraftPath;
    private string BundledPath => Path.Combine(AppContext.BaseDirectory, "defaults", "gestures.wg2");

    internal MainWindow(EngineSettings settings, bool smoke)
    {
        _settings = settings;
        _smoke = smoke;
        Title = "Nyxic Gestures";
        Width = 1160; Height = 800; MinWidth = 780; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        SetResourceReference(BackgroundProperty, "ShellBackground");
        SetResourceReference(ForegroundProperty, "MainText");
        BuildShell();
        try { OpenWorkspace(!smoke && File.Exists(_draftPath) ? _draftPath : BundledPath); }
        catch (Exception e) { Notify("Could not load configuration", e.Message, true); }
        _nav.SelectedIndex = 0;
        Render();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
        Closing += (_, e) =>
        {
            if (_busy) { e.Cancel = true; return; }
            if (!_smoke && _dirty && !Confirm("Unsaved workspace", "Close without saving your workspace edits? The running engine will not change."))
                e.Cancel = true;
        };
        Closed += (_, _) => _statusTimer.Stop();
    }

    private static TextBlock Text(string value, double size = 14, bool bold = false, bool muted = false)
    {
        var text = new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
        text.SetResourceReference(TextBlock.ForegroundProperty, muted ? "MutedText" : "MainText");
        return text;
    }

    private static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel();
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private static void Add(Panel panel, UIElement item, double gap = 12)
    {
        if (item is FrameworkElement el) el.Margin = new Thickness(el.Margin.Left, el.Margin.Top, el.Margin.Right, gap);
        panel.Children.Add(item);
    }

    private static Border Card(string title, params UIElement[] children)
    {
        var panel = new StackPanel();
        if (title.Length != 0) Add(panel, Text(title, 18, true), 14);
        foreach (var child in children) Add(panel, child);
        var border = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(20),
            BorderThickness = new Thickness(1), Child = panel };
        border.SetResourceReference(Border.BackgroundProperty, "CardBackground");
        border.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
        return border;
    }

    private static FrameworkElement Field(string label, FrameworkElement input, string? help = null)
    {
        AutomationProperties.SetName(input, label);
        var title = Text(label, 13, true);
        title.Margin = new Thickness(0, 0, 0, 6);
        var panel = Stack(title, input);
        if (!string.IsNullOrWhiteSpace(help))
        {
            var hint = Text(help, 12, muted: true);
            hint.Margin = new Thickness(0, 5, 0, 0);
            panel.Children.Add(hint);
        }
        return panel;
    }

    private static TextBox Input(string value = "", bool multi = false, int max = 0)
        => new()
        {
            Text = value, MinHeight = multi ? 92 : 34, MaxLength = max,
            Padding = new Thickness(9, 6, 9, 6), AcceptsReturn = multi,
            TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = multi ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden
        };

    private static ComboBox Choice<T>(IEnumerable<T> items, T selected)
    {
        var combo = new ComboBox { ItemsSource = items, MinHeight = 34, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        combo.SelectedItem = selected;
        return combo;
    }

    private Button Button(string label, Action act, bool primary = false)
    {
        var button = MakeButton(label, primary);
        button.Click += (_, _) =>
        {
            if (_busy) return;
            try { act(); } catch (Exception e) { Notify("Operation failed", e.Message, true); }
        };
        return button;
    }

    private Button AsyncButton(string label, Func<Task> act, bool primary = false)
    {
        var button = MakeButton(label, primary);
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            button.IsEnabled = false;
            try { await act(); } catch (Exception e) { Notify("Operation failed", e.Message, true); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    private static Button MakeButton(string label, bool primary = false)
    {
        var button = new Button { Content = label, MinHeight = 35, Padding = new Thickness(14, 7, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Left };
        if (primary)
        {
            button.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
            button.SetResourceReference(Control.ForegroundProperty, "AccentText");
        }
        return button;
    }

    private static WrapPanel Actions(params UIElement[] children)
    {
        var panel = new WrapPanel();
        foreach (var child in children)
        {
            if (child is FrameworkElement el) el.Margin = new Thickness(0, 0, 8, 6);
            panel.Children.Add(child);
        }
        return panel;
    }

    private static CheckBox Check(string label, bool value, Action<bool>? update = null)
    {
        var check = new CheckBox { Content = label, IsChecked = value, Padding = new Thickness(0, 5, 0, 5) };
        if (update is not null)
        {
            check.Checked += (_, _) => update(true);
            check.Unchecked += (_, _) => update(false);
        }
        return check;
    }

    private void BuildShell()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = new DockPanel { LastChildFill = true, Margin = new Thickness(12, 20, 12, 14) };
        var brand = Stack(Text("Nyxic Gestures", 22, true), Text("WINDOWS GESTURE MANAGER", 10, muted: true));
        brand.Margin = new Thickness(10, 6, 0, 26);
        DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);
        var version = Text("0.1.0-rc.3  ·  Release candidate", 11, muted: true);
        version.Margin = new Thickness(10, 10, 0, 4);
        DockPanel.SetDock(version, Dock.Bottom); side.Children.Add(version);
        _nav.BorderThickness = new Thickness(0);
        _nav.Background = Brushes.Transparent;
        _nav.SelectionMode = SelectionMode.Single;
        AutomationProperties.SetName(_nav, "Settings navigation");
        foreach (var item in Pages)
        {
            var icon = new TextBlock { Text = item.Icon, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Width = 30 };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 10, 3, 10) };
            row.Children.Add(icon); row.Children.Add(Text(item.Label, 14));
            _nav.Items.Add(new ListBoxItem { Content = row, Tag = item.Id, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
        _nav.SelectionChanged += (_, _) =>
        {
            if (_nav.SelectedItem is ListBoxItem { Tag: string id }) { _selectedPage = id; Render(); }
        };
        side.Children.Add(_nav);
        var rail = new Border { Child = side, BorderThickness = new Thickness(0,0,1,0) };
        rail.SetResourceReference(Border.BackgroundProperty, "NavBackground");
        rail.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
        root.Children.Add(rail);

        var area = new Grid();
        area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        area.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(area, 1); root.Children.Add(area);

        _pageTitle.FontSize = 30; _pageTitle.FontWeight = FontWeights.SemiBold;
        _pageTitle.SetResourceReference(TextBlock.ForegroundProperty, "MainText");
        _description.FontSize = 14; _description.TextWrapping = TextWrapping.Wrap;
        _description.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        _description.Margin = new Thickness(0, 6, 0, 0);
        var header = Stack(_pageTitle, _description);
        header.Margin = new Thickness(28, 24, 28, 14); area.Children.Add(header);

        _page.Margin = new Thickness(28, 0, 28, 18);
        _scroll.Content = _page; _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Grid.SetRow(_scroll, 1); area.Children.Add(_scroll);

        _notice.TextWrapping = TextWrapping.Wrap; _notice.Visibility = Visibility.Collapsed;
        _notice.Margin = new Thickness(0, 0, 0, 8);
        _sourceText.FontSize = 11; _sourceText.TextTrimming = TextTrimming.CharacterEllipsis;
        _sourceText.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        _engineStatus.FontWeight = FontWeights.SemiBold; _engineStatus.FontSize = 13;
        _engineStatus.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        var footerStack = Stack(_notice, _engineStatus, _sourceText,
            Actions(Button("Save workspace", SaveWorkspace), AsyncButton("Apply & start / reload", ApplyAndStartAsync, true), AsyncButton("Stop engine", StopFromUiAsync)));
        footerStack.Margin = new Thickness(28, 12, 28, 5);
        var footer = new Border { Child = footerStack, BorderThickness = new Thickness(0, 1, 0, 0) };
        footer.SetResourceReference(Border.BackgroundProperty, "CardBackground");
        footer.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
        Grid.SetRow(footer, 2); area.Children.Add(footer);
        Content = root;
    }

    private void OpenWorkspace(string path)
    {
        var loaded = GestureWorkspace.Open(path);
        _workspace = loaded; _source = path; _dirty = false;
        _profile = GestureWorkspace.GlobalKey; _query = "";
    }

    private void Navigate(string id)
    {
        var index = Array.FindIndex(Pages, item => item.Id == id);
        if (index < 0) return;
        if (_nav.SelectedIndex == index) Render();
        else _nav.SelectedIndex = index;
    }

    private void Render()
    {
        _page.Children.Clear(); _scroll.ScrollToTop();
        (_pageTitle.Text, _description.Text) = _selectedPage switch
        {
            "gestures" => ("Gestures", "Create, record and organize mouse gestures."),
            "apps" => ("Applications", "Manage application profiles and global inheritance."),
            "corners" => ("Corners & edges", "Assign actions to the eight screen locations."),
            "settings" => ("Preferences", "Customize input behavior, feedback and appearance."),
            "about" => ("About", "A community-maintained continuation of WGestures."),
            _ => ("Overview", "A local workspace for faster Windows interactions.")
        };
        UpdateStatus();
        if (_selectedPage == "settings") { RenderPreferences(); return; }
        if (_selectedPage == "about") { RenderAbout(); return; }
        if (_workspace is null)
        {
            Add(_page, Card("Open a gesture configuration", Text("Import an original WGestures version-3 file."),
                Button("Import configuration…", ImportWorkspace, true)));
            return;
        }
        switch (_selectedPage)
        {
            case "gestures": RenderGestures(); break;
            case "apps": RenderApps(); break;
            case "corners": RenderCorners(); break;
            default: RenderOverview(); break;
        }
    }

    private void RenderOverview()
    {
        var profiles = _workspace!.Profiles;
        var metrics = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var (number, label) in new[]
        {
            (profiles[0].Gestures.Count, "Global gestures"), (profiles.Count - 1, "App profiles"),
            (profiles.Sum(p => p.Gestures.Count), "Total gestures")
        })
        {
            var card = Card("", Text(number.ToString(), 30, true), Text(label, 13, muted: true));
            card.Margin = new Thickness(0, 0, 8, 0);
            metrics.Children.Add(card);
        }
        Add(_page, metrics);
        Add(_page, Card("Gesture engine", Text("Your configuration stays local. Closing this window does not stop the background engine."),
            Actions(AsyncButton("Apply & start", ApplyAndStartAsync, true), AsyncButton("Stop", StopFromUiAsync),
                Button("Open diagnostics folder", OpenDiagnostics))));
        Add(_page, Card("Your gestures", Text("Draw a gesture or choose an action from the library."),
            Actions(Button("Gesture library", () => Navigate("gestures"), true), Button("Application profiles", () => Navigate("apps")))));
        Add(_page, Card("Configuration", Text("Import, export or restore the original bundled gesture definitions. Active settings change only when you apply them."),
            Actions(Button("Import…", ImportWorkspace), Button("Export…", ExportWorkspace), Button("Restore defaults", RestoreDefaults))));
    }

    private bool Confirm(string title, string message) => MessageBox.Show(this, message, title,
        MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK;

    private void ImportWorkspace()
    {
        var picker = new OpenFileDialog { Filter = "WGestures configuration (*.wg2;*.json)|*.wg2;*.json|All files|*.*" };
        if (picker.ShowDialog(this) != true) return;
        if (_dirty && !Confirm("Replace workspace", "Discard unsaved changes and load the selected configuration?")) return;
        OpenWorkspace(picker.FileName); Changed(); Render();
        Notify("Configuration imported", "The source file remains unchanged. Apply only configurations you trust.");
    }

    private void ExportWorkspace()
    {
        if (_workspace is null) return;
        var picker = new SaveFileDialog { Filter = "WGestures configuration (*.wg2)|*.wg2", DefaultExt = ".wg2",
            FileName = "Nyxic-gestures.wg2", OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) return;
        if (string.Equals(Path.GetFullPath(picker.FileName), Path.GetFullPath(EngineSettings.ActivePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use Apply & start to change the active configuration.");
        _workspace.Export(picker.FileName);
        Notify("Configuration exported", picker.FileName);
    }

    private void RestoreDefaults()
    {
        if (!Confirm("Restore default gestures", "Replace the workspace with the bundled gesture definitions? The active configuration will not change until applied.")) return;
        OpenWorkspace(BundledPath); Changed(); Render();
    }

    private void SaveWorkspace()
    {
        if (_workspace is null) { Notify("Nothing to save", "Import a configuration first.", true); return; }
        _settings.Validate();
        _workspace.SaveDraft(_draftPath);
        _settings.Save(EngineSettings.PreferencesPath);
        _source = _draftPath; _dirty = false; UpdateStatus();
        Notify("Workspace saved", "Apply changes to reload the background engine.");
    }

    private void Changed() { _dirty = true; UpdateStatus(); }

    private void UpdateStatus()
    {
        _engineStatus.Text = _busy ? "Applying configuration…" : EngineReady() ? "Engine running · Input hooks installed" :
            EngineRunning() ? "Engine starting / stopping" : "Engine stopped";
        _sourceText.Text = (_dirty ? "Unsaved edits · " : "Workspace · ") + _source;
    }

    private void Notify(string title, string message, bool error = false)
    {
        _notice.Text = title + "  ·  " + message;
        _notice.SetResourceReference(TextBlock.ForegroundProperty, error ? "MainText" : "AccentBrush");
        _notice.Visibility = Visibility.Visible;
        if (error) Program.Log(title + ": " + message);
    }

    private void OpenDiagnostics()
    {
        Directory.CreateDirectory(EngineSettings.DataDirectory);
        Process.Start(new ProcessStartInfo(EngineSettings.DataDirectory) { UseShellExecute = true })?.Dispose();
    }

    private void RenderAbout()
    {
        Add(_page, Card("Nyxic Gestures", Text("0.1.0-rc.3 Release candidate", 22, true),
            Text("Modern .NET 10 + WPF settings application with an independent native gesture engine."),
            Text($"Windows build: {Theme.Profile.WindowsVersion} · UI mode: {Theme.CurrentMode}"),
            Text("Windows 10 compatibility is selected automatically. Minimum target: version 1607 (build 14393)."),
            Text("Based on WGestures by YingDev, GPL-2.0. No account or telemetry is required."),
            Actions(Button("Project source", () => Process.Start(new ProcessStartInfo("https://github.com/Nyxic-Lab/WGestures") { UseShellExecute = true })?.Dispose()),
                Button("Original project", () => Process.Start(new ProcessStartInfo("https://github.com/yingDev/WGestures") { UseShellExecute = true })?.Dispose()),
                Button("Local diagnostics", OpenDiagnostics))));
    }

    internal void RunSmokeChecks()
    {
        if (_workspace is null || _workspace.Profiles.Sum(p => p.Gestures.Count) != 66)
            throw new InvalidOperationException("Bundled configuration missing or invalid.");
        foreach (var page in Pages)
        {
            Navigate(page.Id); UpdateLayout();
            if (_page.Children.Count == 0) throw new InvalidOperationException("Empty page: " + page.Id);
            Program.Log("PASS WPF page: " + page.Id);
        }
        foreach (var command in CommandCatalog.Definitions)
        {
            var editor = BuildCommandEditor(CommandCatalog.Create(command.Id));
            _ = editor.Read();
        }
        var draftEditor = BuildGestureForm(null);
        if (draftEditor.View.Children.Count == 0) throw new InvalidOperationException("Gesture editor is empty.");
        Theme.Apply("Dark"); Navigate("gestures"); UpdateLayout();
        Theme.Apply("Light"); Navigate("overview"); UpdateLayout();
        Theme.Apply("System");
    }
}
