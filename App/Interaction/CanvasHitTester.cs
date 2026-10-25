using System;
using System.Linq;
using Avalonia;
using PolygonStudio.App.Rendering;
using PolygonStudio.App.Viewport;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.App.Interaction;

public sealed class CanvasHitTester(Scene scene, ViewTransform transform)
{
    private const double VertexHitTolerance = 10.0;
    private const double ControlPointHitTolerance = 10.0;
    private const double EdgeHitTolerance = 8.0;
    
    public (Polygon Polygon, Vertex Vertex)? FindVertexAt(Point screenPoint)
    {
        (Polygon Polygon, Vertex Vertex)? bestHit = null;
        double bestDistanceSquared = VertexHitTolerance * VertexHitTolerance;

        foreach (var polygon in scene.Polygons)
        foreach (var vertex in polygon.Vertices)
        {
            var screenVertex = transform.WorldToCanvas(vertex.Position);

            double dx = screenPoint.X - screenVertex.X;
            double dy = screenPoint.Y - screenVertex.Y;
            double distanceSquared = dx * dx + dy * dy;

            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestHit = (polygon, vertex);
        }

        return bestHit;
    }

    public (Polygon Polygon, Edge Edge, int Index)? FindControlPointAt(Point screenPoint)
    {
        (Polygon Polygon, Edge Edge, int Index)? bestHit = null;
        double bestDistanceSquared = ControlPointHitTolerance * ControlPointHitTolerance;
        
        foreach (var polygon in scene.Polygons)
        foreach (var edge in polygon.Edges)
        {
            if (edge.Geometry is not CubicBezierGeometry bezier)
                continue;
            
            Point[] controlPoints =
            [
                transform.WorldToCanvas(bezier.ControlPoint1),
                transform.WorldToCanvas(bezier.ControlPoint2)
            ];
            
            for (var i = 0; i < controlPoints.Length; i++)
            {
                var point = controlPoints[i];

                double dx = point.X - screenPoint.X;
                double dy = point.Y - screenPoint.Y;
                double distanceSquared = dx * dx + dy * dy;
                
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestHit = (polygon, edge, i);
            }
        }

        return bestHit;
    }
    
    public (Polygon Polygon, Edge Edge)? FindEdgeAt(Point screenPoint)
    {
        (Polygon Polygon, Edge Edge)? bestHit = null;
        double bestDistanceSquared = EdgeHitTolerance * EdgeHitTolerance;
        
        foreach (var polygon in scene.Polygons)
        foreach (var edge in polygon.Edges)
        {
            var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
            var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);

            if (from is null || to is null)
                continue;

            var samples = EdgeGeometrySampler.Sample(
                edge.Geometry, from.Position, to.Position
            );
            
            Point2D? prev = null;
            foreach (var sample in samples)
            {
                if (prev is null)
                {
                    prev = sample;
                    continue;
                }
                
                var a = transform.WorldToCanvas(prev.Value);
                var b = transform.WorldToCanvas(sample);

                double distanceSquared = DistanceSquaredToSegment(screenPoint, a, b);

                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestHit = (polygon, edge);

                prev = sample;
            }
        }

        return bestHit;
    }

    // ---
    
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
            0.0, 1.0
        );

        double closestX = a.X + t * dx;
        double closestY = a.Y + t * dy;
        double distX = point.X - closestX;
        double distY = point.Y - closestY;

        return distX * distX + distY * distY;
    }
}