namespace WGestures.Modern.Core;

/// <summary>
/// Product compatibility policy. This is a runtime minimum, not a claim that
/// Microsoft still services every Windows edition or that every build was tested.
/// </summary>
public sealed record WindowsCompatibilityProfile(
    Version WindowsVersion, bool MeetsMinimum, bool UseFluent, bool UsePerMonitorV2)
{
    public string UiMode => UseFluent ? "Fluent" : "Classic WPF compatibility";
}

public static class WindowsCompatibility
{
    public const int MinimumBuild = 14393; // Windows 10 1607 / Server 2016
    public const int PerMonitorV2Build = 15063; // Windows 10 1703
    public const int FluentBuild = 22000; // Windows 11

    public static WindowsCompatibilityProfile ForVersion(Version version, bool forceCompatibility = false)
    {
        bool minimum = version >= new Version(10, 0, MinimumBuild);
        return new WindowsCompatibilityProfile(version, minimum,
            minimum && !forceCompatibility && version >= new Version(10, 0, FluentBuild),
            minimum && !forceCompatibility && version >= new Version(10, 0, PerMonitorV2Build));
    }

    public static WindowsCompatibilityProfile Current(bool forceCompatibility = false) =>
        ForVersion(Environment.OSVersion.Version, forceCompatibility);
}
