using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WGestures.Modern;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string section) return;

        (string title, string description, string card) = section switch
        {
            "gestures" => ("Gestures", "Create, inspect and organize mouse gestures.", "Gesture editor"),
            "apps" => ("Applications", "Manage per-app gesture profiles and overrides.", "Application profiles"),
            "corners" => ("Corners & edges", "Configure hot corners and edge actions.", "Corner actions"),
            "settings" => ("Preferences", "Choose input behavior, visuals and shortcuts.", "Input preferences"),
            "about" => ("About", "A community-maintained modernization of the original WGestures.", "Project information"),
            _ => ("Overview", "Mouse gestures, designed for today's Windows.", "Modernization in progress")
        };
        PageEyebrow.Text = "WGESTURES / " + section.ToUpperInvariant();
        PageTitle.Text = title;
        PageDescription.Text = description;
        CardTitle.Text = card;
        CardDescription.Text = "This section is the new WinUI 3 shell. Editing and persistence will be enabled after the original settings models and engine are safely migrated.";
    }
}
