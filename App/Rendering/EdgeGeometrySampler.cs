using System;
using System.Collections.Generic;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.App.Rendering;

public static class EdgeGeometrySampler
{
    private const int BezierSegments = 64;
    
    private const double MaxArcAngleStep = Math.PI / 64;
    private const double MinimumRadius = 1e-9;

    public static IEnumerable<Point2D> Sample(EdgeGeometry geometry, Point2D start, Point2D end)
    {
        return geometry switch
        {
            LineGeometry => SampleLine(start, end),

            CubicBezierGeometry bezier => SampleBezier(start, end, bezier),

            CircularArcGeometry arc => SampleArc(start, end, arc),

            _ => throw new NotSupportedException($"Unsupported edge geometry: {geometry.GetType().Name}")
        };
    }

    // ---
    
    private static IEnumerable<Point2D> SampleLine(Point2D start, Point2D end)
    {
        yield return start;
        yield return end;
    }
    
    private static IEnumerable<Point2D> SampleBezier(
        Point2D start, Point2D end,
        CubicBezierGeometry bezier)
    {
        var cp1 = bezier.ControlPoint1;
        var cp2 = bezier.ControlPoint2;
        
        // Convert to power-basis
        var a = new Point2D(
            -start.X + 3.0 * cp1.X - 3.0 * cp2.X + end.X,
            -start.Y + 3.0 * cp1.Y - 3.0 * cp2.Y + end.Y
        );

        var b = new Point2D(
            3.0 * start.X - 6.0 * cp1.X + 3.0 * cp2.X,
            3.0 * start.Y - 6.0 * cp1.Y + 3.0 * cp2.Y
        );

        var c = new Point2D(
            -3.0 * start.X + 3.0 * cp1.X,
            -3.0 * start.Y + 3.0 * cp1.Y
        );

        const double h = 1.0 / BezierSegments;
        const double h2 = h * h;
        const double h3 = h2 * h;
        
        var current = start;

        var firstDiff = new Point2D(
            a.X * h3 + b.X * h2 + c.X * h,
            a.Y * h3 + b.Y * h2 + c.Y * h
        );

        var secondDiff = new Point2D(
            6.0 * a.X * h3 + 2.0 * b.X * h2,
            6.0 * a.Y * h3 + 2.0 * b.Y * h2
        );

        var thirdDiff = new Point2D(
            6.0 * a.X * h3,
            6.0 * a.Y * h3
        );

        yield return start;

        for (var i = 1; i < BezierSegments; i++)
        {
            current += firstDiff;
            firstDiff += secondDiff;
            secondDiff += thirdDiff;

            yield return current;
        }

        yield return end;
    }
    
    private static IEnumerable<Point2D> SampleArc(
        Point2D start,
        Point2D end,
        CircularArcGeometry arc)
    {
        if (!CircularArcMath.TryGetParameters(start, end, arc, out var parameters)
            || parameters.Radius < MinimumRadius)
        {
            foreach (var point in SampleLine(start, end))
                yield return point;

            yield break;
        }

        int segments = Math.Max(
            2,
            (int)Math.Ceiling(Math.Abs(parameters.SweepAngleRadians) / MaxArcAngleStep)
        );

        yield return start;

        for (var i = 1; i < segments; i++)
        {
            double t = (double)i / segments;
            double angle = parameters.StartAngleRadians + parameters.SweepAngleRadians * t;

            yield return new Point2D(
                parameters.Center.X + parameters.Radius * Math.Cos(angle),
                parameters.Center.Y + parameters.Radius * Math.Sin(angle)
            );
        }

        yield return end;
    }
}