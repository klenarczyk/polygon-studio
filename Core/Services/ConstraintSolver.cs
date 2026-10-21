using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services;

public sealed class ConstraintSolver(Scene scene)
{
    private const double ConstraintWeight = 1000.0;
    private const double TargetWeight = 1000.0;
    private const double PreservationWeight = 10;
    private const double ControlPointPreservationWeight = 0.1;
    
    private const double LengthTolerance = 1e-5;
    private const double ContinuityTolerance = 1e-5;
    private const double TargetTolerance = 1e-4;

    private readonly LeastSquaresSolver _numericalSolver = new();

    public bool TryMoveTarget(MoveTarget target, Point2D requestedPosition)
    {
        var layout = BuildLayout();

        if (!TryGetTargetIndices(target, layout, out int targetX))
            return false;

        var fixedConstraints = ResolveFixedConstraints();
        if (fixedConstraints is null)
            return false;

        var continuityConstraints = ResolveContinuities();
        if (continuityConstraints is null)
            return false;

        double[] initial = layout.Initial;
        double[] candidate;

        try
        {
            var res = _numericalSolver.Solve(
                initial,
                values => Residuals(
                    values,
                    layout,
                    fixedConstraints,
                    continuityConstraints,
                    targetX,
                    requestedPosition)
            );

            if (!res.Converged)
                return false;

            candidate = res.Variables;

            if (!ValidateCandidate(
                    candidate,
                    layout,
                    fixedConstraints,
                    continuityConstraints,
                    targetX,
                    requestedPosition))
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }

        Commit(candidate, layout);
        return true;
    }
    
    private VariableLayout BuildLayout()
    {
        var vertices = new Dictionary<VertexKey, int>();
        var beziers = new Dictionary<EdgeKey, (int Cp1, int Cp2)>();
        var arcs = new Dictionary<EdgeKey, int>();
        var values = new List<double>();

        foreach (var polygon in scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                int x = Add(vertex.Position.X);
                Add(vertex.Position.Y);

                vertices.Add(new VertexKey(polygon.Id, vertex.Id), x);
            }

            foreach (var edge in polygon.Edges)
            {
                var key = new EdgeKey(polygon.Id, edge.Id);

                switch (edge.Geometry)
                {
                    case CubicBezierGeometry bezier:
                    {
                        int cp1 = Add(bezier.ControlPoint1.X);
                        Add(bezier.ControlPoint1.Y);

                        int cp2 = Add(bezier.ControlPoint2.X);
                        Add(bezier.ControlPoint2.Y);

                        beziers.Add(key, (cp1, cp2));
                        break;
                    }

                    case CircularArcGeometry arc:
                        arcs.Add(key, Add(arc.Bulge));
                        break;
                }
            }
        }

        return new VariableLayout(vertices, beziers, arcs, values.ToArray());

        // ---
        
        int Add(double value)
        {
            values.Add(value);
            return values.Count - 1;
        }
    }

    private static bool TryGetTargetIndices(MoveTarget target, VariableLayout layout, out int xIndex)
    {
        xIndex = -1;

        if (target.Kind == MoveTargetKind.Vertex)
        {
            var key = new VertexKey(target.PolygonId, target.ElementId);
            return layout.Vertices.TryGetValue(key, out xIndex);
        }

        if (target.Kind == MoveTargetKind.BezierControlPoint)
        {
            if (target.ControlPointIndex is not (0 or 1))
                return false;

            var key = new EdgeKey(target.PolygonId, target.ElementId);

            if (!layout.Beziers.TryGetValue(key, out var indices))
                return false;

            xIndex = target.ControlPointIndex == 0
                ? indices.Cp1
                : indices.Cp2;

            return true;
        }

        return false;
    }
    
    private List<ResolvedFixedConstraint>? ResolveFixedConstraints()
    {
        var result = new List<ResolvedFixedConstraint>();

        foreach (var constraint in scene.Constraints.OfType<FixedEdgeLengthConstraint>())
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

    private List<ResolvedContinuity>? ResolveContinuities()
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
                vertex.Continuity,
                incoming!, outgoing!)
            );
        }
        
        return result;
    }

    private static double[] Residuals(
        double[] values,
        VariableLayout layout,
        IReadOnlyList<ResolvedFixedConstraint> fixedConstraints,
        IReadOnlyList<ResolvedContinuity> continuities,
        int targetX,
        Point2D requestedPosition)
    {
        var residuals = new List<double>();

        foreach (var constraint in fixedConstraints)
        {
            int from = layout.Vertices[constraint.FromKey];
            int to = layout.Vertices[constraint.ToKey];

            double dx = values[to] - values[from];
            double dy = values[to + 1] - values[from + 1];
            
            double length = Math.Sqrt(dx * dx + dy * dy);
            
            residuals.Add(ConstraintWeight * (length - constraint.EdgeLength));
        }
        
        foreach (var continuity in continuities)
        {
            var incoming = Derivative(
                values, layout, continuity.Polygon,
                continuity.Incoming, true
            );

            var outgoing = Derivative(
                values, layout, continuity.Polygon,
                continuity.Outgoing, false
            );

            if (continuity.Type == ContinuityType.G1)
            {
                double aLength = Length(incoming);
                double bLength = Length(outgoing);

                if (aLength < 1e-12 || bLength < 1e-12)
                {
                    residuals.Add(ConstraintWeight);
                    continue;
                }

                // Co-linear -> Cross product == 0
                residuals.Add(ConstraintWeight * Cross(incoming, outgoing) / (aLength * bLength));
            }
            else if (continuity.Type == ContinuityType.C1)
            {
                residuals.Add(ConstraintWeight * (incoming.X - outgoing.X));
                residuals.Add(ConstraintWeight * (incoming.Y - outgoing.Y));
            }
        }
        
        residuals.Add(TargetWeight * (values[targetX] - requestedPosition.X));
        residuals.Add(TargetWeight * (values[targetX + 1] - requestedPosition.Y));
        
        for (var i = 0; i < values.Length; i++)
        {
            if (i == targetX || i == targetX + 1)
                continue;

            bool isControlPoint = layout.Beziers.Values.Any(cp =>
                i == cp.Cp1 || 
                i == cp.Cp1 + 1 || 
                i == cp.Cp2 || 
                i == cp.Cp2 + 1
            );
            
            double weight = isControlPoint
                ? ControlPointPreservationWeight
                : PreservationWeight;

            residuals.Add(weight * (values[i] - layout.Initial[i]));
        }
        
        return [.. residuals];
    }

    private static bool ValidateCandidate(
        double[] values,
        VariableLayout layout,
        IReadOnlyList<ResolvedFixedConstraint> fixedConstraints,
        IReadOnlyList<ResolvedContinuity> continuities,
        int targetX,
        Point2D requestedPosition)
    {
        if (values.Length != layout.Initial.Length)
            return false;

        double targetDx = values[targetX] - requestedPosition.X;
        double targetDy = values[targetX + 1] - requestedPosition.Y;

        if (targetDx * targetDx + targetDy * targetDy > TargetTolerance * TargetTolerance)
            return false;
        
        foreach (var constraint in fixedConstraints)
        {
            int from = layout.Vertices[constraint.FromKey];
            int to = layout.Vertices[constraint.ToKey];

            double dx = values[to] - values[from];
            double dy = values[to + 1] - values[from + 1];
            double length = Math.Sqrt(dx * dx + dy * dy);

            double tolerance = LengthTolerance * Math.Max(1.0, constraint.EdgeLength);

            if (Math.Abs(length - constraint.EdgeLength) > tolerance)
                return false;
        }

        foreach (var continuity in continuities)
        {
            var a = Derivative(
                values, layout, continuity.Polygon,
                continuity.Incoming, true
            );

            var b = Derivative(
                values, layout, continuity.Polygon,
                continuity.Outgoing, false
            );

            double la = Length(a);
            double lb = Length(b);

            if (la < 1e-12 || lb < 1e-12)
                return false;

            if (continuity.Type == ContinuityType.G1)
            {
                double normalizedCross = Math.Abs(Cross(a, b)) / (la * lb);

                if (normalizedCross > ContinuityTolerance || Dot(a, b) <= 0)
                    return false;
            }
            else if (continuity.Type == ContinuityType.C1)
            {
                double error = Length(new Point2D(a.X - b.X, a.Y - b.Y));

                if (error > ContinuityTolerance * Math.Max(1.0, Math.Max(la, lb)))
                    return false;
            }
        }

        return true;
    }
    
    private void Commit(double[] values, VariableLayout layout)
    {
        foreach (var polygon in scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                int i = layout.Vertices[new VertexKey(polygon.Id, vertex.Id)];
                vertex.Position = new Point2D(values[i], values[i + 1]);
            }

            foreach (var edge in polygon.Edges)
            {
                var key = new EdgeKey(polygon.Id, edge.Id);

                if (edge.Geometry is CubicBezierGeometry bezier)
                {
                    var indices = layout.Beziers[key];

                    edge.Geometry = bezier
                        .WithControlPoint(0, new Point2D(
                            values[indices.Cp1],
                            values[indices.Cp1 + 1]))
                        .WithControlPoint(1, new Point2D(
                            values[indices.Cp2],
                            values[indices.Cp2 + 1]));
                }
                else if (edge.Geometry is CircularArcGeometry)
                {
                    edge.Geometry = new CircularArcGeometry(values[layout.Arcs[key]]);
                }
            }
        }
    }
    
    // ---
    
    private static Point2D Derivative(
        double[] values,
        VariableLayout layout,
        Polygon polygon, Edge edge,
        bool atEnd)
    {
        int from = layout.Vertices[new VertexKey(polygon.Id, edge.FromVertexId)];
        int to = layout.Vertices[new VertexKey(polygon.Id, edge.ToVertexId)];

        var p0 = new Point2D(values[from], values[from + 1]);
        var p3 = new Point2D(values[to], values[to + 1]);

        switch (edge.Geometry)
        {
            case LineGeometry:
                return new Point2D(p3.X - p0.X, p3.Y - p0.Y);

            case CubicBezierGeometry:
            {
                var indices = layout.Beziers[new EdgeKey(polygon.Id, edge.Id)];

                var cp1 = new Point2D(values[indices.Cp1], values[indices.Cp1 + 1]);
                var cp2 = new Point2D(values[indices.Cp2], values[indices.Cp2 + 1]);

                return atEnd
                    ? new Point2D(3 * (p3.X - cp2.X), 3 * (p3.Y - cp2.Y))
                    : new Point2D(3 * (cp1.X - p0.X), 3 * (cp1.Y - p0.Y));
            }

            case CircularArcGeometry:
            {
                double bulge = values[layout.Arcs[new EdgeKey(polygon.Id, edge.Id)]];

                double chordAngle = Math.Atan2(p3.Y - p0.Y, p3.X - p0.X);

                double tangentAngle = chordAngle + (atEnd ? 2.0 : -2.0) * Math.Atan(bulge);

                return new Point2D(
                    Math.Cos(tangentAngle),
                    Math.Sin(tangentAngle)
                );
            }

            default:
                throw new NotSupportedException($"Unsupported edge geometry: {edge.Geometry.GetType().Name}");
        }
    }

    private static double Length(Point2D p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

    private static double Dot(Point2D a, Point2D b) => a.X * b.X + a.Y * b.Y;
    
    private static double Cross(Point2D a, Point2D b) => a.X * b.Y - a.Y * b.X;
    
    // ---
    
    private readonly record struct VertexKey(Guid PolygonId, Guid VertexId);
    private readonly record struct EdgeKey(Guid PolygonId, Guid EdgeId);

    private sealed record VariableLayout(
        Dictionary<VertexKey, int> Vertices,
        Dictionary<EdgeKey, (int Cp1, int Cp2)> Beziers,
        Dictionary<EdgeKey, int> Arcs,
        double[] Initial
    );
    
    private sealed record ResolvedFixedConstraint(
        VertexKey FromKey,
        VertexKey ToKey,
        double EdgeLength
    );

    private sealed record ResolvedContinuity(
        Polygon Polygon,
        ContinuityType Type,
        Edge Incoming,
        Edge Outgoing
    );
}
