using System;
using Avalonia;
using PolygonStudio.Core.Geometry;

namespace PolygonStudio.App.Viewport;

public readonly record struct ViewTransform
{
    public double Scale { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }
    public double OriginX { get; }
    public double OriginY { get; }

    public ViewTransform(double scale, double offsetX, double offsetY, double originX, double originY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        Scale = scale;
        OffsetX = offsetX;
        OffsetY = offsetY;
        OriginX = originX;
        OriginY = originY;
    }
    
    public Point WorldToCanvas(Point2D point) =>
        new(
            OffsetX + (point.X - OriginX) * Scale,
            OffsetY + (OriginY - point.Y) * Scale
        );

    public Point2D CanvasToWorld(Point point) =>
        new(
            OriginX + (point.X - OffsetX) / Scale,
            OriginY - (point.Y - OffsetY) / Scale
        );

    public Point2D CanvasDeltaToWorld(Point delta) =>
        new(delta.X / Scale, -delta.Y / Scale);

    public ViewTransform PanBy(Point canvasDelta) =>
        new(
            Scale,
            OffsetX + canvasDelta.X,
            OffsetY + canvasDelta.Y,
            OriginX,
            OriginY
        );

    public ViewTransform ZoomAt(Point canvasPosition, double factor)
    {
        if (factor <= 0) return this;

        double newScale = Math.Clamp(Scale * factor, 0.01, 1000.0);
        var worldAnchor = CanvasToWorld(canvasPosition);

        return new ViewTransform(
            newScale,
            canvasPosition.X - (worldAnchor.X - OriginX) * newScale,
            canvasPosition.Y - (OriginY - worldAnchor.Y) * newScale,
            OriginX,
            OriginY
        );
    }
}