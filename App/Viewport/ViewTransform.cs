using System;
using Avalonia;
using PolygonStudio.Core.Geometry;

namespace PolygonStudio.App.Viewport;

public readonly record struct ViewTransform
{
    private readonly double _scale;
    private readonly double _offsetX;
    private readonly double _offsetY;
    private readonly double _originX;
    private readonly double _originY;

    public ViewTransform(double scale, double offsetX, double offsetY, double originX, double originY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        _scale = scale;
        _offsetX = offsetX;
        _offsetY = offsetY;
        _originX = originX;
        _originY = originY;
    }
    
    public Point WorldToCanvas(Point2D point) =>
        new(
            _offsetX + (point.X - _originX) * _scale,
            _offsetY + (_originY - point.Y) * _scale
        );

    public Point2D CanvasToWorld(Point point) =>
        new(
            _originX + (point.X - _offsetX) / _scale,
            _originY - (point.Y - _offsetY) / _scale
        );

    public Point2D CanvasDeltaToWorld(Point delta) =>
        new(delta.X / _scale, -delta.Y / _scale);

    public ViewTransform PanBy(Point canvasDelta) =>
        new(
            _scale,
            _offsetX + canvasDelta.X,
            _offsetY + canvasDelta.Y,
            _originX, _originY
        );

    public ViewTransform ZoomAt(Point canvasPosition, double factor)
    {
        if (factor <= 0) return this;

        double newScale = Math.Clamp(_scale * factor, 0.5, 8.0);
        var worldAnchor = CanvasToWorld(canvasPosition);

        return new ViewTransform(
            newScale,
            canvasPosition.X - (worldAnchor.X - _originX) * newScale,
            canvasPosition.Y - (_originY - worldAnchor.Y) * newScale,
            _originX, _originY
        );
    }
}