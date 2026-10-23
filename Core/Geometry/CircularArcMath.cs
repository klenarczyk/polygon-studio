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
    private const double MinimumChordLength = 1e-9;
    private const double MinimumBulgeMagnitude = 1e-9;
    
    public static bool TryGetParameters(
        Point2D start, Point2D end,
        CircularArcGeometry arc,
        out CircularArcParameters parameters)
    {
        parameters = default;
        
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chordLength = Math.Sqrt(dx * dx + dy * dy);
        double bulge = arc.Bulge;

        if (chordLength < MinimumChordLength || Math.Abs(bulge) < MinimumBulgeMagnitude)
            return false;

        // Chord midpoint
        double midpointX = (start.X + end.X) / 2.0;
        double midpointY = (start.Y + end.Y) / 2.0;
        
        // Distance between chord midpoint and circle center
        double offset = chordLength * (1.0 - bulge * bulge) / (4.0 * bulge);
        
        double nx = -dy / chordLength;
        double ny = dx / chordLength;

        // Circle center
        var center = new Point2D(
            midpointX + nx * offset,
            midpointY + ny * offset
        );

        double radius = chordLength * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge));

        double startAngle = Math.Atan2(
            start.Y - center.Y,
            start.X - center.X
        );

        // Angular sweep
        double sweep = 4.0 * Math.Atan(bulge);

        parameters = new CircularArcParameters(center, radius, startAngle, sweep);
        return true;
    }
}