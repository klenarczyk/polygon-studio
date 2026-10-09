using Avalonia.Controls;
using PolygonStudio.App.Controls;
using PolygonStudio.Core.Model;

namespace PolygonStudio.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var canvas = this.FindControl<PolygonCanvas>("Canvas")!;
        canvas.Scene = new Scene();
    }
}