using Avalonia;
using PolygonStudio.Core.Geometry;

namespace PolygonStudio.App.Controls.Canvas;

public readonly record struct ViewTransform(
    double Scale,   // Screen:World ratio
    double Left,    // Horizontal offset
    double Top,     // Vertical offset
    double MinX,    // Min world x
    double MaxY)    // Max world y
{
    public Point ToScreen(Point2D point) =>
        new(
            Left + (point.X - MinX) * Scale,
            Top + (MaxY - point.Y) * Scale
        );

    public Point2D ToWorld(Point point) =>
        new(
            MinX + (point.X - Left) / Scale,
            MaxY - (point.Y - Top) / Scale
        );
}