using Avalonia.Controls;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var scene = new Scene();
        var editorService = new EditorService(scene);
        
        Canvas.Scene = scene;
        Canvas.Editor = editorService;
    }
}