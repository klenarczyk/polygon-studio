using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services.Constraints;

public static class ConstraintResolver
{
    internal static List<ResolvedFixedConstraint>? ResolveFixed(Scene scene)
    {
        var result = new List<ResolvedFixedConstraint>();

        foreach (var constraint in scene.Constraints.OfType<FixedLengthConstraint>())
        {
            var polygon = scene.Polygons.FirstOrDefault(p => p.Id == constraint.PolygonId);
            var edge = polygon?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId);

            if (polygon is null || edge is null)
                return null;

            var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
            var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);

            if (from is null || to is null)
                return null;

            result.Add(new ResolvedFixedConstraint(
                new VertexKey(polygon.Id, from.Id),
                new VertexKey(polygon.Id, to.Id),
                constraint.Length)
            );
        }

        return result;
    }

    internal static List<ResolvedPerpendicularConstraint>? ResolvePerpendicular(Scene scene)
    {
        var result = new List<ResolvedPerpendicularConstraint>();

        foreach (var constraint in scene.Constraints.OfType<PerpendicularConstraint>())
        {
            var polygon1 = scene.Polygons.FirstOrDefault(p => p.Id == constraint.PolygonId1);
            var polygon2 = scene.Polygons.FirstOrDefault(p => p.Id == constraint.PolygonId2);

            var edge1 = polygon1?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId1);
            var edge2 = polygon2?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId2);

            if (polygon1 is null || polygon2 is null || edge1 is null || edge2 is null)
                return null;

            if (edge1.Geometry is not LineGeometry || edge2.Geometry is not LineGeometry)
                return null;

            var from1 = polygon1.Vertices.FirstOrDefault(v => v.Id == edge1.FromVertexId);
            var to1 = polygon1.Vertices.FirstOrDefault(v => v.Id == edge1.ToVertexId);

            var from2 = polygon2.Vertices.FirstOrDefault(v => v.Id == edge2.FromVertexId);
            var to2 = polygon2.Vertices.FirstOrDefault(v => v.Id == edge2.ToVertexId);

            if (from1 is null || to1 is null || from2 is null || to2 is null)
                return null;

            result.Add(new ResolvedPerpendicularConstraint(
                new VertexKey(polygon1.Id, from1.Id),
                new VertexKey(polygon1.Id, to1.Id),
                new VertexKey(polygon2.Id, from2.Id),
                new VertexKey(polygon2.Id, to2.Id))
            );
        }

        return result;
    }

    internal static List<ResolvedContinuity>? ResolveContinuities(Scene scene)
    {
        var result = new List<ResolvedContinuity>();

        foreach (var polygon in scene.Polygons)
        foreach (var vertex in polygon.Vertices)
        {
            if (vertex.Continuity == ContinuityType.G0)
                continue;

            if (!ContinuityHelper.CanApply(polygon, vertex.Id, vertex.Continuity))
                return null;

            if (!ContinuityHelper.TryResolveAdjacentEdges(
                    polygon, vertex.Id,
                    out var incoming, out var outgoing))
            {
                return null;
            }

            result.Add(new ResolvedContinuity(
                polygon,
                vertex.Id,
                vertex.Continuity,
                incoming!, outgoing!)
            );
        }

        return result;
    }
}