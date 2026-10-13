using PolygonStudio.Core.Geometry;

namespace PolygonStudio.Core.Model.Geometries;

public static class EdgeGeometryFactory
{
    public static EdgeGeometry CreateDefault(
        Point2D start, Point2D end,
        Type geometryType)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;

        return geometryType switch
        {
            _ when geometryType == typeof(LineGeometry) => new LineGeometry(),

            _ when geometryType == typeof(CubicBezierGeometry) =>
                new CubicBezierGeometry(
                    new Point2D(start.X + dx / 3.0, start.Y + dy / 3.0),
                    new Point2D(start.X + 2 * dx / 3.0, start.Y + 2 * dy / 3.0)
                ),

            _ when geometryType == typeof(CircularArcGeometry) => CircularArcGeometry.CreateSemicircle(),

            _ => throw new ArgumentOutOfRangeException(
                    nameof(geometryType), geometryType, "Unsupported geometry type."
                )
        };
    }
}