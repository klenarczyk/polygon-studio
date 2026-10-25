using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services;

public static class ContinuityHelper
{
    public static bool TryResolveAdjacentEdges(
        Polygon polygon, Guid vertexId,
        out Edge? incoming, out Edge? outgoing)
    {
        incoming = polygon.Edges.SingleOrDefault(e => e.ToVertexId == vertexId);
        outgoing = polygon.Edges.SingleOrDefault(e => e.FromVertexId == vertexId);
        
        return polygon.Vertices.Any(v => v.Id == vertexId)
            && incoming is not null
            && outgoing is not null
            && incoming.Id != outgoing.Id;
    }

    public static bool CanApply(Polygon? polygon, Guid vertexId, ContinuityType type)
    {
        if (polygon is null)
            return false;
        
        if (!TryResolveAdjacentEdges(polygon, vertexId, out var incoming, out var outgoing))
            return false;

        if (type == ContinuityType.G0)
            return true;

        var a = incoming!.Geometry;
        var b = outgoing!.Geometry;

        if (type == ContinuityType.C1)
        {
            return (a, b) is
                (LineGeometry, CubicBezierGeometry) or
                (CubicBezierGeometry, LineGeometry) or
                (CubicBezierGeometry, CubicBezierGeometry);
        }

        bool supported = (a, b) is
            (CubicBezierGeometry, CubicBezierGeometry) or
            (LineGeometry, CubicBezierGeometry) or
            (CubicBezierGeometry, LineGeometry) or
            (CircularArcGeometry, CubicBezierGeometry) or
            (CubicBezierGeometry, CircularArcGeometry) or
            (CircularArcGeometry, LineGeometry) or
            (LineGeometry, CircularArcGeometry) or
            (CircularArcGeometry, CircularArcGeometry);

        if (!supported) return false;
        
        var arcIds = new HashSet<Guid>();

        if (a is CircularArcGeometry) arcIds.Add(incoming.Id);
        if (b is CircularArcGeometry) arcIds.Add(outgoing.Id);

        if (arcIds.Count == 0)
            return true;

        return !polygon.Vertices.Any(v =>
            v.Id != vertexId &&
            v.Continuity == ContinuityType.G1 &&
            TryResolveAdjacentEdges(polygon, v.Id, out var otherIncoming, out var otherOutgoing) &&
            (arcIds.Contains(otherIncoming!.Id) || arcIds.Contains(otherOutgoing!.Id))
        );
    }

    public static void NormalizeArcBulges(Polygon polygon)
    {
        foreach (var edge in polygon.Edges)
        {
            if (edge.Geometry is not CircularArcGeometry arc)
                continue;
            
            var start = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
            var end = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);
            
            if (start is null || end is null)
                continue;
            
            if (start.Continuity != ContinuityType.G0 || end.Continuity != ContinuityType.G0)
                continue;

            double bulge = arc.Bulge < 0 ? -1.0 : 1.0;

            if (Math.Abs(Math.Abs(arc.Bulge) - 1.0) > 1e-12)
                edge.Geometry = new CircularArcGeometry(bulge);
        }
    }
    
    public static void ResetInvalid(Scene scene)
    {
        foreach (var polygon in scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                if (!CanApply(polygon, vertex.Id, vertex.Continuity))
                    vertex.Continuity = ContinuityType.G0;
            }
            
            NormalizeArcBulges(polygon);
        }
    }
}