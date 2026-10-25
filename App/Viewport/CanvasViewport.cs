using System;
using System.Linq;
using Avalonia;
using PolygonStudio.Core.Model;

namespace PolygonStudio.App.Viewport;

public sealed class CanvasViewport
{
    private const double Padding = 30.0;
    private const double MinimumWorldSize = 1e-6;

    public ViewTransform? Transform { get; private set; }

    public void FitToScene(Scene scene, Size viewportSize)
    {
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
            return;

        var points = scene.Polygons
            .SelectMany(polygon => polygon.Vertices)
            .Select(vertex => vertex.Position)
            .ToList();

        if (points.Count == 0)
        {
            Transform = new ViewTransform(
                1, 
                viewportSize.Width / 2, viewportSize.Height / 2, 
                0, 0
            );
            
            return;
        }

        double minX = points.Min(point => point.X);
        double maxX = points.Max(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxY = points.Max(point => point.Y);

        double worldWidth = Math.Max(maxX - minX, MinimumWorldSize);
        double worldHeight = Math.Max(maxY - minY, MinimumWorldSize);

        double availableWidth = Math.Max(viewportSize.Width - 2 * Padding, 1);
        double availableHeight = Math.Max(viewportSize.Height - 2 * Padding, 1);

        double scale = Math.Min(
            availableWidth / worldWidth,
            availableHeight / worldHeight
        );

        double drawnWidth = worldWidth * scale;
        double drawnHeight = worldHeight * scale;

        double left = (viewportSize.Width - drawnWidth) / 2;
        double top = (viewportSize.Height - drawnHeight) / 2;

        Transform = new ViewTransform(
            scale,
            left,
            top,
            minX,
            maxY
        );
    }

    public void PanBy(Point canvasDelta)
    {
        Transform = Transform?.PanBy(canvasDelta);
    }

    public void ZoomAt(Point canvasPosition, double factor)
    {
        Transform = Transform?.ZoomAt(canvasPosition, factor);
    }
}