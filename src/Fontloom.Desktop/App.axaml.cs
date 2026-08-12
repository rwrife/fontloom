using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fontloom.Core.Organization;
using Fontloom.Core.Specimens;
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
            var organizationStore = new JsonFontOrganizationStore();
            var catalogService = new SystemFontCatalogService(organizationStore);
            var specimenExporter = new SkiaSpecimenExporter();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(catalogService, organizationStore, specimenExporter)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
