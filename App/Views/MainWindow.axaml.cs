using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PolygonStudio.App.Controls;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Views;

public partial class MainWindow : Window
{
    private EditorService _editorService;
    
    private readonly Guid _polygonId;
    private readonly Guid _vertexId;
    private int _turn;
    
    public MainWindow()
    {
        InitializeComponent();
        
        var canvas = this.FindControl<PolygonCanvas>("Canvas")!;
        canvas.Scene = new Scene();
        
        _editorService = new EditorService(canvas.Scene);

        _polygonId = canvas.Scene.Polygons[0].Id;
        _vertexId = canvas.Scene.Polygons[0].Vertices[1].Id;
    }
    
    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var newPosition = _turn == 0 ? new Point2D(0.0, -1.0) : new Point2D(0.0, 1.0);

        if (_editorService.MoveVertex(_polygonId, _vertexId, newPosition))
        {
            _turn = 1 - _turn;
            this.FindControl<PolygonCanvas>("Canvas")!.InvalidateVisual();
        }
    }
}