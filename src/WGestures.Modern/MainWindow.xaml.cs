using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WGestures.Modern.Core;

namespace WGestures.Modern;

public sealed partial class MainWindow : Window
{
    private GestureWorkspace? _workspace;
    private string _source = "No document loaded";
    private bool _dirty;
    private string _page = "overview";
    private string _profile = GestureWorkspace.GlobalKey;
    private string _query = "";
    private bool _ready;
    private readonly string _draftPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nyxic-Lab", "Gestures", "workspace.wg2");
    private string BundledPath => Path.Combine(AppContext.BaseDirectory, "defaults", "gestures.wg2");

    public MainWindow()
    {
        InitializeComponent();
        _ready = true;
        try
        {
            var path = File.Exists(_draftPath) ? _draftPath : BundledPath;
            OpenWorkspace(path);
        }
        catch (Exception e)
        {
            Notify("Could not load a gesture workspace", e.Message, InfoBarSeverity.Error);
        }
        Nav.SelectedItem = Nav.MenuItems[0];
        Render();
    }

    private void OpenWorkspace(string path)
    {
        var loaded = GestureWorkspace.Open(path);
        _workspace = loaded;
        _source = path;
        _dirty = false;
        _profile = GestureWorkspace.GlobalKey;
        _query = "";
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_ready || args.SelectedItemContainer?.Tag is not string section) return;
        _page = section;
        Render();
    }

    private void Render()
    {
        PageHost.Children.Clear();
        (PageTitle.Text, PageDescription.Text) = _page switch
        {
            "gestures" => ("Gestures", "Find and edit the gestures in your configuration draft."),
            "apps" => ("Applications", "Inspect application-specific profiles and global inheritance."),
            "corners" => ("Corners & edges", "Inspect the eight screen corner and edge command slots."),
            "settings" => ("Preferences", "Personalize the settings interface."),
            "about" => ("About", "A community-maintained continuation of the original WGestures."),
            _ => ("Overview", "Make everyday Windows interactions a little faster.")
        };
        UpdateSource();
        if (_page == "settings") { RenderPreferences(); return; }
        if (_page == "about") { RenderAbout(); return; }
        if (_workspace is null)
        {
            PageHost.Children.Add(Heading("Open a gesture configuration", 20));
            PageHost.Children.Add(Paragraph("Choose an original WGestures version-3 .wg2 file to start."));
            PageHost.Children.Add(ImportButton());
            return;
        }

        switch (_page)
        {
            case "gestures": RenderGestures(); break;
            case "apps": RenderApps(); break;
            case "corners": RenderCorners(); break;
            default: RenderOverview(); break;
        }
    }

    private static TextBlock Heading(string text, double size = 18) => new()
    {
        Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock Paragraph(string text) => new()
    {
        Text = text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Opacity = 0.8
    };

    private Border Card(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var child in children) panel.Children.Add(child);
        return new Border { Style = (Style)Root.Resources["Surface"], Child = panel };
    }

    private Button Action(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, MinHeight = 36 };
        if (primary) button.Style = (Style)Root.Resources["PrimaryAction"];
        button.Click += (_, _) => action();
        return button;
    }

    private Button ImportButton()
    {
        var button = new Button { Content = "Import configuration…", MinHeight = 36 };
        button.Click += async (_, _) =>
        {
            if (_dirty)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Root.XamlRoot, Title = "Unsaved draft",
                    Content = "Save your changes before importing, or discard them.",
                    PrimaryButtonText = "Discard and import", CloseButtonText = "Cancel"
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            try
            {
                var picker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                picker.FileTypeFilter.Add(".wg2");
                picker.FileTypeFilter.Add(".json");
                var file = await picker.PickSingleFileAsync();
                if (file is null) return;
                OpenWorkspace(file.Path);
                Render();
                Notify("Configuration loaded", "The original file remains unchanged.", InfoBarSeverity.Success);
            }
            catch (Exception e)
            {
                Notify("Import failed", e.Message, InfoBarSeverity.Error);
            }
        };
        return button;
    }

    private void SaveDraft()
    {
        if (_workspace is null) return;
        try
        {
            _workspace.SaveDraft(_draftPath);
            _source = _draftPath;
            _dirty = false;
            UpdateSource();
            Notify("Draft saved", "The legacy application and its configuration were not modified.", InfoBarSeverity.Success);
        }
        catch (Exception e) { Notify("Save failed", e.Message, InfoBarSeverity.Error); }
    }

    private void UpdateSource() =>
        SourceText.Text = (_dirty ? "Unsaved changes · " : "Source · ") + _source;

    private void Notify(string title, string message, InfoBarSeverity severity)
    {
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private void Navigate(string key)
    {
        var item = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => Equals(i.Tag, key));
        if (item is not null)
        {
            if (ReferenceEquals(Nav.SelectedItem, item)) Render();
            else Nav.SelectedItem = item;
        }
    }

    private void RenderOverview()
    {
        var profiles = _workspace!.Profiles;
        var global = profiles.Single(p => p.Key == GestureWorkspace.GlobalKey);
        var metrics = new Grid { ColumnSpacing = 10 };
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        var cards = new[]
        {
            Card(Heading(global.Gestures.Count.ToString(), 28), Paragraph("Global gestures")),
            Card(Heading((profiles.Count - 1).ToString(), 28), Paragraph("App profiles")),
            Card(Heading(profiles.Sum(p => p.Gestures.Count).ToString(), 28), Paragraph("Total gestures"))
        };
        for (int i = 0; i < cards.Length; i++)
        {
            Grid.SetColumn(cards[i], i);
            metrics.Children.Add(cards[i]);
        }
        PageHost.Children.Add(metrics);
        var info = new InfoBar
        {
            IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational,
            Title = "Independent configuration workspace",
            Message = "The current mouse hook and command engine have not been migrated. Changes here are saved only as a draft."
        };
        PageHost.Children.Add(info);
        PageHost.Children.Add(Card(Heading("Your gestures"), Paragraph("Browse global gestures and per-application assignments."),
            Action("Open gesture library", () => Navigate("gestures"), true)));
        PageHost.Children.Add(Card(Heading("Configuration"), Paragraph("Start from the bundled original defaults or load your own version-3 file."),
            ImportButton(), Action("Save draft", SaveDraft)));
    }

    private void RenderGestures()
    {
        var profiles = _workspace!.Profiles;
        var picker = new ComboBox { Header = "Profile", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var p in profiles)
            picker.Items.Add(new ComboBoxItem { Content = p.Name + $"  ({p.Gestures.Count})", Tag = p.Key });
        picker.SelectedIndex = Math.Max(0, profiles.ToList().FindIndex(p => p.Key == _profile));
        var search = new TextBox { Header = "Search gestures", PlaceholderText = "Name, direction, action…", Text = _query };
        var list = new ListView
        {
            ItemTemplate = (DataTemplate)Root.Resources["GestureRowTemplate"],
            SelectionMode = ListViewSelectionMode.Single,
            MinHeight = 180, MaxHeight = 440,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(list, "Gesture library");
        var results = Paragraph("");
        var editor = new StackPanel { Spacing = 12 };
        var editorCard = Card(Heading("Gesture details"), editor);
        var listCard = Card(picker, search, results, list);
        var columns = new Grid { ColumnSpacing = 14, RowSpacing = 14 };
        columns.Children.Add(listCard);
        columns.Children.Add(editorCard);
        void Layout(double width)
        {
            columns.ColumnDefinitions.Clear();
            columns.RowDefinitions.Clear();
            if (width >= 750)
            {
                columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
                columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(editorCard, 1);
                Grid.SetRow(editorCard, 0);
            }
            else
            {
                columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(editorCard, 0);
                Grid.SetRow(editorCard, 1);
            }
        }
        Layout(PageHost.ActualWidth - 56);
        columns.SizeChanged += (_, e) =>
        {
            bool needsTwo = e.NewSize.Width >= 750;
            if (needsTwo != (columns.ColumnDefinitions.Count == 2)) Layout(e.NewSize.Width);
        };
        PageHost.Children.Add(columns);
        PageHost.Children.Add(Card(Heading("Draft storage"), ImportButton(), Action("Save draft", SaveDraft, true)));

        void Refresh(int? selectIndex = null)
        {
            var rows = _workspace.Profiles.First(p => p.Key == _profile).Gestures
                .Where(r => string.IsNullOrWhiteSpace(_query) ||
                    (r.Name + " " + r.Summary + " " + r.ModifierLabel).Contains(_query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            list.ItemsSource = rows;
            results.Text = $"{rows.Length} matching gestures";
            if (rows.Length > 0)
                list.SelectedItem = rows.FirstOrDefault(r => r.Index == selectIndex) ?? rows[0];
            else
            {
                editor.Children.Clear();
                editor.Children.Add(Paragraph("No matching gestures."));
            }
        }

        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is ComboBoxItem { Tag: string key }) { _profile = key; Refresh(); }
        };
        search.TextChanged += (_, _) => { _query = search.Text; Refresh(); };
        list.SelectionChanged += (_, _) =>
        {
            editor.Children.Clear();
            if (list.SelectedItem is not GestureRow row)
            {
                editor.Children.Add(Paragraph("Select a gesture to inspect or edit it."));
                return;
            }
            editor.Children.Add(Heading(row.Name, 20));
            editor.Children.Add(Paragraph($"Trigger: {row.TriggerLabel} · Modifier: {row.ModifierLabel}"));
            editor.Children.Add(Paragraph($"Command: {row.Command}"));
            var name = new TextBox { Header = "Gesture name", Text = row.Name, MaxLength = 120 };
            var path = new TextBox { Header = "Directions", Text = row.Path, MaxLength = 32 };
            editor.Children.Add(name);
            editor.Children.Add(path);
            var directionKeys = new Grid { ColumnSpacing = 5, RowSpacing = 5 };
            for (int i = 0; i < 3; i++)
            {
                directionKeys.ColumnDefinitions.Add(new ColumnDefinition());
                directionKeys.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            var keys = new[] { "↖", "↑", "↗", "←", "⌫", "→", "↙", "↓", "↘" };
            for (int i = 0; i < keys.Length; i++)
            {
                string arrow = keys[i];
                var key = Action(arrow, () =>
                {
                    if (arrow == "⌫")
                    {
                        if (path.Text.Length > 0) path.Text = path.Text[..^1];
                    }
                    else if (path.Text.Length < 32) path.Text += arrow;
                });
                key.HorizontalAlignment = HorizontalAlignment.Stretch;
                AutomationProperties.SetName(key, arrow == "⌫" ? "Remove last direction" : $"Append {arrow}");
                Grid.SetRow(key, i / 3);
                Grid.SetColumn(key, i % 3);
                directionKeys.Children.Add(key);
            }
            editor.Children.Add(directionKeys);
            editor.Children.Add(Action("Apply edit to draft", () =>
            {
                try
                {
                    _workspace.EditGesture(row.ProfileKey, row.Index, name.Text, path.Text);
                    _dirty = true;
                    UpdateSource();
                    Refresh(row.Index);
                    Notify("Draft updated", "Select Save draft to keep this change.", InfoBarSeverity.Success);
                }
                catch (Exception e) { Notify("Invalid gesture", e.Message, InfoBarSeverity.Error); }
            }, true));
            editor.Children.Add(Paragraph("Trigger, modifiers and command parameters are preserved. Editing those fields is not implemented yet."));
        };
        Refresh();
    }

    private void RenderApps()
    {
        foreach (var profile in _workspace!.Profiles)
        {
            var key = profile.Key;
            PageHost.Children.Add(Card(
                Heading(profile.Name),
                Paragraph(profile.ExecutablePath ?? "Shared default profile"),
                Paragraph($"{profile.Gestures.Count} gestures · {(profile.Enabled ? "Enabled in source" : "Disabled in source")}" +
                    (profile.InheritsGlobal ? " · Inherits global gestures" : "")),
                Action("View gestures", () => { _profile = key; _query = ""; Navigate("gestures"); })));
        }
    }

    private void RenderCorners()
    {
        PageHost.Children.Add(Paragraph("Slots are shown in their original stored order. Command execution and editing are not connected."));
        var rows = _workspace!.Corners;
        foreach (var row in rows)
            PageHost.Children.Add(Card(Heading(row.Name), Paragraph(row.Command)));
    }

    private void RenderPreferences()
    {
        var theme = new ComboBox { Header = "Appearance", HorizontalAlignment = HorizontalAlignment.Stretch };
        theme.Items.Add("System default");
        theme.Items.Add("Light");
        theme.Items.Add("Dark");
        theme.SelectedIndex = Root.RequestedTheme switch { ElementTheme.Light => 1, ElementTheme.Dark => 2, _ => 0 };
        theme.SelectionChanged += (_, _) =>
            Root.RequestedTheme = theme.SelectedIndex switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        PageHost.Children.Add(Card(Heading("Personalization"), Paragraph("Choose the interface theme for this session."), theme));
        PageHost.Children.Add(Card(Heading("Local configuration"), Paragraph(_draftPath), ImportButton(),
            Action("Save draft", SaveDraft)));
    }

    private void RenderAbout()
    {
        PageHost.Children.Add(Card(Heading("Nyxic Gestures", 24), Paragraph("Working name · Modern workspace preview"),
            Paragraph("A continuation of WGestures by YingDev, under the original GPL-2.0 license."),
            Paragraph(".NET 10 · WinUI 3 · Windows App SDK 2.7.0"),
            new HyperlinkButton { Content = "Project source", NavigateUri = new Uri("https://github.com/Nyxic-Lab/WGestures") },
            new HyperlinkButton { Content = "Original project", NavigateUri = new Uri("https://github.com/yingDev/WGestures") }));
    }
}
