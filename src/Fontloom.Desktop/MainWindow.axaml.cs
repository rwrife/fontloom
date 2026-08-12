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

    private void ToggleFavorite_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ToggleSelectedFavorite();
        }
    }

    private void SaveTags_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveSelectedFontTags();
        }
    }

    private void CreateCollection_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CreateCollection();
        }
    }

    private void ToggleCollectionMembership_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ToggleSelectedFontCollectionMembership();
        }
    }

    private void AddLooseFontFolder_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AddLooseFontFolder();
        }
    }

    private void RemoveLooseFontFolder_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.RemoveSelectedLooseFontFolder();
        }
    }

    private void ToggleComparisonPin_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ToggleSelectedFontComparisonPin();
        }
    }

    private void ExportSelectedFontPng_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExportSelectedFontSpecimenPng();
        }
    }

    private void ExportSelectedCollectionPdf_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExportSelectedCollectionSpecimenPdf();
        }
    }
}
