using Avalonia.Controls;
using Avalonia.Interactivity;
using PolygonStudio.App.Rendering;
using PolygonStudio.App.ViewModels;

namespace PolygonStudio.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        
        Canvas.Scene = viewModel.Scene;
        Canvas.Editor = viewModel.Editor;
    }
    
    private void OnAvaloniaLineDrawingClick(object? sender, RoutedEventArgs e)
    {
        Canvas.LineDrawingAlgorithm = LineDrawingAlgorithm.Avalonia;
    }

    private void OnBresenhamLineDrawingClick(object? sender, RoutedEventArgs e)
    {
        Canvas.LineDrawingAlgorithm = LineDrawingAlgorithm.Bresenham;
    }

    private void OnResetSceneClick(object? sender, RoutedEventArgs e)
    {
        Canvas.Scene?.Reset();
        Canvas.InvalidateVisual();
    }

    private async void OnDocumentationClick(object? sender, RoutedEventArgs e)
    {
        var documentation = new DocumentationWindow();
        await documentation.ShowDialog(this);
    }
}