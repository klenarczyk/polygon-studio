using System;
using System.Linq;
using Avalonia;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;

namespace PolygonStudio.App.Controls.Canvas;

public sealed class CanvasHitTester(
    Scene scene, 
    ViewTransform transform, 
    double edgeHitTolerance = 8.0)
{
    public (Polygon Polygon, Edge Edge)? FindEdgeAt(Point screenPoint)
    {
        (Polygon Polygon, Edge Edge)? bestHit = null;
        double bestDistanceSquared = edgeHitTolerance * edgeHitTolerance;
        
        foreach (var polygon in scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
                var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);

                if (from is null || to is null)
                    continue;

                var samples = EdgeGeometrySampler.Sample(
                    edge.Geometry, from.Position, to.Position
                );

                for (var i = 1; i < samples.Count; i++)
                {
                    var a = transform.ToScreen(samples[i - 1]);
                    var b = transform.ToScreen(samples[i]);

                    double distanceSquared = DistanceSquaredToSegment(screenPoint, a, b);

                    if (distanceSquared >= bestDistanceSquared)
                        continue;

                    bestDistanceSquared = distanceSquared;
                    bestHit = (polygon, edge);
                }
            }
        }

        return bestHit;
    }

    private static double DistanceSquaredToSegment(Point point, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 1e-9)
        {
            double px = point.X - a.X;
            double py = point.Y - a.Y;
            return px * px + py * py;
        }

        double t = Math.Clamp(
            ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared,
            0.0, 1.0);

        double closestX = a.X + t * dx;
        double closestY = a.Y + t * dy;
        double distX = point.X - closestX;
        double distY = point.Y - closestY;

        return distX * distX + distY * distY;
    }
}