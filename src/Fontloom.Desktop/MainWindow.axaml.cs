using Avalonia.Controls;
using Fontloom.Desktop.ViewModels;

namespace Fontloom.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ReloadFonts_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ReloadFonts();
        }
    }
}
