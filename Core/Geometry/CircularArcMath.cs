using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Geometry;

public readonly record struct CircularArcParameters(
    Point2D Center,
    double Radius,
    double StartAngleRadians,
    double SweepAngleRadians
);

public static class CircularArcMath
{
    public static bool TryGetParameters(
        Point2D start, Point2D end,
        CircularArcGeometry arc,
        out CircularArcParameters parameters)
    {
        parameters = default;
        
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chordLength = Math.Sqrt(dx * dx + dy * dy);
        double b = arc.Bulge;

        if (chordLength < 1e-9 || Math.Abs(b) < 1e-9)
            return false;

        // Chord midpoint
        double midpointX = (start.X + end.X) / 2.0;
        double midpointY = (start.Y + end.Y) / 2.0;
        
        // Distance between chord midpoint and circle center
        double offset = chordLength * (1.0 - b * b) / (4.0 * b);
        
        double nx = -dy / chordLength;
        double ny = dx / chordLength;

        // Circle center
        var center = new Point2D(
            midpointX + nx * offset,
            midpointY + ny * offset);

        double radius = chordLength * (1.0 + b * b) / (4.0 * Math.Abs(b));

        double startAngle = Math.Atan2(
            start.Y - center.Y,
            start.X - center.X);

        // Angular sweep
        double sweep = 4.0 * Math.Atan(b);

        parameters = new CircularArcParameters(
            center, radius, startAngle, sweep);

        return true;
    }
}