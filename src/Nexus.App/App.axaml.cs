using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nexus.App.Services;

namespace Nexus.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = AppPaths.Create();
            var audit = new AuditLog(paths.AuditLogPath);
            var database = new AppDatabase(paths.DatabasePath);
            database.Initialize();
            desktop.MainWindow = new MainWindow(paths, database, audit);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
