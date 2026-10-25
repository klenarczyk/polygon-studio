using Avalonia.Controls;
using PolygonStudio.App.ViewModels;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        
        Canvas.Scene = _viewModel.Scene;
        Canvas.Editor = _viewModel.Editor;
    }
}