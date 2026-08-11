using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fontloom.Desktop.Services;
using Fontloom.Desktop.ViewModels;

namespace Fontloom.Desktop;

public partial class App : Application
{
    public override void Initialize()
        => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new SystemFontCatalogService())
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
