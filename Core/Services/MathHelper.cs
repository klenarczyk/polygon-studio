using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services.Constraints;

namespace PolygonStudio.Core.Services;

internal static class MathHelper
{
    internal static Point2D EvaluateDerivative(SolverState state, Polygon polygon, Edge edge, bool atEnd)
    {
        var p0 = state.Vertices[new VertexKey(polygon.Id, edge.FromVertexId)];
        var p3 = state.Vertices[new VertexKey(polygon.Id, edge.ToVertexId)];

        switch (edge.Geometry)
        {
            case LineGeometry:
                return new Point2D(p3.X - p0.X, p3.Y - p0.Y);

            case CubicBezierGeometry:
            {
                var (cp1, cp2) = state.Beziers[new EdgeKey(polygon.Id, edge.Id)];
                return atEnd
                    ? new Point2D(3.0 * (p3.X - cp2.X), 3.0 * (p3.Y - cp2.Y))
                    : new Point2D(3.0 * (cp1.X - p0.X), 3.0 * (cp1.Y - p0.Y));
            }

            case CircularArcGeometry:
            {
                double bulge = state.Arcs[new EdgeKey(polygon.Id, edge.Id)];
                double chordAngle = Math.Atan2(p3.Y - p0.Y, p3.X - p0.X);
                double tangentAngle = chordAngle + (atEnd ? 2.0 : -2.0) * Math.Atan(bulge);

                return new Point2D(Math.Cos(tangentAngle), Math.Sin(tangentAngle));
            }

            default:
                throw new NotSupportedException($"Unsupported edge geometry: {edge.Geometry.GetType().Name}");
        }
    }

    internal static double NormalizeAngle(double angle)
    {
        while (angle <= -Math.PI) angle += 2.0 * Math.PI;
        while (angle > Math.PI) angle -= 2.0 * Math.PI;
        return angle;
    }

    internal static double Length(Point2D p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

    internal static double Distance(Point2D a, Point2D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    internal static double DistanceSquared(Point2D a, Point2D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    internal static double Dot(Point2D a, Point2D b) => a.X * b.X + a.Y * b.Y;

    internal static double Cross(Point2D a, Point2D b) => a.X * b.Y - a.Y * b.X;
    
    // ---
    
    internal static double CalculateBulgeFromTangent(Point2D start, Point2D end, Point2D tangent, bool atStart)
    {
        double chordAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double tangentAngle = Math.Atan2(tangent.Y, tangent.X);

        double sweepHalf = atStart
            ? NormalizeAngle(chordAngle - tangentAngle)
            : NormalizeAngle(tangentAngle - chordAngle);

        return Math.Tan(sweepHalf / 2.0);
    }
}