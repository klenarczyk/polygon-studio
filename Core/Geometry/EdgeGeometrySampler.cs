using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Geometry;

public static class EdgeGeometrySampler
{
    private const int DefaultBezierSegments = 64;
    private const double MaxArcAngleStep = Math.PI / 64;
    private const double MinimumRadius = 1e-9;

    public static IReadOnlyList<Point2D> Sample(
        EdgeGeometry geometry,
        Point2D start, Point2D end,
        int bezierSegments = DefaultBezierSegments)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentOutOfRangeException.ThrowIfLessThan(bezierSegments, 1);

        int numSegments = geometry switch
        {
            LineGeometry => 1,

            CubicBezierGeometry => bezierSegments,

            CircularArcGeometry arc 
                when CircularArcMath.TryGetParameters(start, end, arc, out var parameters) 
                     && parameters.Radius >= MinimumRadius =>
                Math.Max(2, (int)Math.Ceiling(Math.Abs(parameters.SweepAngleRadians) / MaxArcAngleStep)),
            
            CircularArcGeometry => 1,

            _ => throw new NotSupportedException($"Unsupported edge geometry: {geometry.GetType().Name}")
        };
        
        var points = new Point2D[numSegments + 1];
        points[0] = start;

        for (var i = 1; i < numSegments + 1; i++)
        {
            double t = (double)i / numSegments;
            points[i] = Evaluate(geometry, start, end, t);
        }
        
        points[numSegments] = end;
        return points;
    }

    private static Point2D Evaluate(
        EdgeGeometry geometry,
        Point2D start, Point2D end,
        double t)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        
        t = Math.Clamp(t, 0.0, 1.0);

        if (t <= 0.0) return start;
        if (t >= 1.0) return end;

        switch (geometry)
        {
            case LineGeometry:
                return Lerp(start, end, t);

            case CubicBezierGeometry bezier:
            {
                double u = 1.0 - t;
                double a = u * u * u;
                double b = 3.0 * u * u * t;
                double c = 3.0 * u * t * t;
                double d = t * t * t;

                return new Point2D(
                    a * start.X + b * bezier.ControlPoint1.X + c * bezier.ControlPoint2.X + d * end.X,
                    a * start.Y + b * bezier.ControlPoint1.Y + c * bezier.ControlPoint2.Y + d * end.Y
                    );
            }

            case CircularArcGeometry arc
                when CircularArcMath.TryGetParameters(start, end, arc, out var parameters)
                     && parameters.Radius >= MinimumRadius:
            {
                double angle = parameters.StartAngleRadians + parameters.SweepAngleRadians * t;

                return new Point2D(
                    parameters.Center.X + parameters.Radius * Math.Cos(angle),
                    parameters.Center.Y + parameters.Radius * Math.Sin(angle)
                    );
            }
            
            case CircularArcGeometry:
                return Lerp(start, end, t);
            
            default:
                throw new NotSupportedException($"Unsupported edge geometry: {geometry.GetType().Name}");
        }
    }
    
    private static Point2D Lerp(Point2D a, Point2D b, double t) =>
        new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );
}