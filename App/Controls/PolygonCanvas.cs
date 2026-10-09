using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;

namespace PolygonStudio.App.Controls;

public sealed class PolygonCanvas : Control
{
    public Scene? Scene { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        double scale = Math.Min(Bounds.Width / 2.0, Bounds.Height) * 0.8;
        
        var pen = new Pen(Brushes.Black, 2);
        
        // Edges
        foreach (var polygon in Scene.Polygons)
        {
            var vertices = polygon.Vertices;
            if (vertices.Count < 3) continue;

            foreach (var edge in polygon.Edges)
            {
                var startVert = vertices.First(v => v.Id == edge.FromVertexId);
                var endVert =  vertices.First(v => v.Id == edge.ToVertexId);
                
                var start = ToScreen(startVert.Position, scale);
                var end = ToScreen(endVert.Position, scale);
                
                context.DrawLine(pen, start, end);
            }
        }
        
        // Vertices
        const double vertexRadius = 5;

        foreach (var polygon in Scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var point = ToScreen(vertex.Position, scale);
                
                context.DrawEllipse(
                    Brushes.White,
                    pen,
                    point,
                    vertexRadius,
                    vertexRadius);
            }
        }
    }
    
    // ---

    private Point ToScreen(Point2D position, double scale)
    {
        return new Point(
            Bounds.Width / 2.0 + position.X * scale,
            Bounds.Height / 2.0 - position.Y * scale);
    }
}