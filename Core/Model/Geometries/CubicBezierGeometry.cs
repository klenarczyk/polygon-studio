using PolygonStudio.Core.Geometry;

namespace PolygonStudio.Core.Model.Geometries;

public record CubicBezierGeometry(
    Point2D ControlPoint1,
    Point2D ControlPoint2
) : EdgeGeometry;