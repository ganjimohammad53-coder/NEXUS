using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.App.Models;

namespace Nexus.App;

/// <summary>Shared application state for the Alpha shell. Page composition stays in the view for this first slice.</summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentPage = "Dashboard";

    [ObservableProperty]
    private string statusMessage = "Local-first mode active";

    public IReadOnlyList<ModuleInfo> Modules { get; } =
    [
        new("AI Center", "Private AI workspace", "Disabled in Alpha", "✧"),
        new("System Center", "Runtime and host details", "Partial", "▦"),
        new("File Manager", "Read-only metadata browsing", "Read-only", "▤"),
        new("Productivity", "Local tasks", "Available", "☷"),
        new("Security", "Permission foundation and backups", "Partial", "◈"),
        new("Automation", "Automation recipes", "Disabled in Alpha", "⌘"),
        new("Gaming", "Game library and tuning", "Planned", "▷"),
        new("Network", "Network diagnostics", "Planned", "⌁"),
        new("Customization", "Preferences and plugins", "Partial", "⚙")
    ];
}
