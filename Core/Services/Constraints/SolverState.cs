using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services.Constraints;

internal sealed record SolverState(
    Dictionary<VertexKey, Point2D> Vertices,
    Dictionary<EdgeKey, (Point2D Cp1, Point2D Cp2)> Beziers,
    Dictionary<EdgeKey, double> Arcs,
    Dictionary<VertexKey, List<(EdgeKey Edge, bool IsFromVertex)>> VertexAdjacentBeziers,
    List<ResolvedFixedConstraint> FixedConstraints,
    List<ResolvedPerpendicularConstraint> PerpendicularConstraints,
    List<ResolvedContinuity> Continuities)
{
    internal static SolverState? Capture(Scene scene)
    {
        var vertices = new Dictionary<VertexKey, Point2D>();
        var beziers = new Dictionary<EdgeKey, (Point2D Cp1, Point2D Cp2)>();
        var arcs = new Dictionary<EdgeKey, double>();
        var vertexAdjacentBeziers = new Dictionary<VertexKey, List<(EdgeKey Edge, bool IsFromVertex)>>();

        foreach (var polygon in scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
                vertices[new VertexKey(polygon.Id, vertex.Id)] = vertex.Position;

            foreach (var edge in polygon.Edges)
            {
                var edgeKey = new EdgeKey(polygon.Id, edge.Id);

                switch (edge.Geometry)
                {
                    case CubicBezierGeometry bezier:
                    {
                        beziers[edgeKey] = (bezier.ControlPoint1, bezier.ControlPoint2);

                        var fromKey = new VertexKey(polygon.Id, edge.FromVertexId);
                        var toKey = new VertexKey(polygon.Id, edge.ToVertexId);

                        if (!vertexAdjacentBeziers.TryGetValue(fromKey, out var fromList))
                            vertexAdjacentBeziers[fromKey] = fromList = [];
                        
                        fromList.Add((edgeKey, true));

                        if (!vertexAdjacentBeziers.TryGetValue(toKey, out var toList))
                            vertexAdjacentBeziers[toKey] = toList = [];
                        
                        toList.Add((edgeKey, false));
                        break;
                    }
                    
                    case CircularArcGeometry arc:
                        arcs[edgeKey] = arc.Bulge;
                        break;
                }
            }
        }

        var fixedConstraints = ConstraintResolver.ResolveFixed(scene);
        if (fixedConstraints is null) return null;

        var perpConstraints = ConstraintResolver.ResolvePerpendicular(scene);
        if (perpConstraints is null) return null;

        var continuities = ConstraintResolver.ResolveContinuities(scene);
        if (continuities is null) return null;

        return new SolverState(
            vertices,
            beziers,
            arcs,
            vertexAdjacentBeziers,
            fixedConstraints,
            perpConstraints,
            continuities
        );
    }
}