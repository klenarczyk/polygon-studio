using PolygonStudio.Core.Geometry;

namespace PolygonStudio.Core.Model.Geometries;

public record CubicBezierGeometry(
    Point2D ControlPoint1,
    Point2D ControlPoint2
) : EdgeGeometry
{
    public CubicBezierGeometry WithControlPoint(int index, Point2D position)
    {
        return index switch 
        { 
            0 => this with { ControlPoint1 = position }, 
            1 => this with { ControlPoint2 = position }, 
            _ => throw new ArgumentOutOfRangeException(nameof(index)) 
        };
    }
};