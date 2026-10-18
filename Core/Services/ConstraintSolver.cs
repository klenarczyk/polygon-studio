using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;

namespace PolygonStudio.Core.Services;

public sealed class ConstraintSolver(Scene scene)
{
    private const double ConstraintWeight = 1000.0;
    private const double PreservationWeight = 0.1;
    private const double LengthTolerance = 1e-5;

    private readonly LeastSquaresSolver _numericalSolver = new();

    public bool TryMoveVertex(Guid polygonId, Guid vertexId, Point2D requestedPosition)
    {
        var polygon = scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var vertex = polygon?.Vertices.FirstOrDefault(v => v.Id == vertexId);

        if (polygon is null || vertex is null)
            return false;

        // TODO: Add more constraint types
        if (scene.Constraints.Any(c => c is not FixedEdgeLengthConstraint))
            return false;

        var fixedConstraints = ResolveFixedConstraints();
        if (fixedConstraints is null)
            return false;

        var targetKey = new VertexKey(polygonId, vertexId);
        var connectedVertices = FindConnectedVertices(targetKey, fixedConstraints);

        // No constraints affect the vertex.
        if (connectedVertices.Count == 1)
        {
            vertex.Position = requestedPosition;
            return true;
        }

        var verticesByKey = connectedVertices.ToDictionary(
            vertexKey => vertexKey,
            vertexKey => FindVertex(vertexKey)!
        );

        if (verticesByKey.Values.Any(_ => false))
            return false;

        var componentConstraints = fixedConstraints
            .Where(c => 
                connectedVertices.Contains(c.FromKey) && 
                connectedVertices.Contains(c.ToKey))
            .ToList();

        if (componentConstraints.Count == 0)
        {
            vertex.Position = requestedPosition;
            return true;
        }

        var keys = connectedVertices.ToArray();
        var initial = new double[keys.Length * 2];

        for (var i = 0; i < keys.Length; i++)
        {
            var position = verticesByKey[keys[i]].Position;
            initial[2 * i] = position.X;
            initial[2 * i + 1] = position.Y;
        }

        var indexByKey = keys
            .Select((key, index) => (key, index))
            .ToDictionary(item => item.key, item => item.index);

        int targetIndex = indexByKey[targetKey];

        LeastSquaresResult result;

        try
        {
            result = _numericalSolver.Solve(initial, Residuals);
        }
        catch (Exception)
        {
            return false;
        }

        if (!result.Converged)
            return false;

        // Constraint verification
        foreach (var constraint in componentConstraints)
        {
            int fromIndex = indexByKey[constraint.FromKey] * 2;
            int toIndex = indexByKey[constraint.ToKey] * 2;

            double dx = result.Variables[toIndex] - result.Variables[fromIndex];
            double dy = result.Variables[toIndex + 1] - result.Variables[fromIndex + 1];

            double actualLength = Math.Sqrt(dx * dx + dy * dy);
            double tolerance = LengthTolerance * Math.Max(1.0, constraint.Length);

            if (Math.Abs(actualLength - constraint.Length) > tolerance)
                return false;
        }

        // Commit changes
        for (var i = 0; i < keys.Length; i++)
        {
            verticesByKey[keys[i]].Position = new Point2D(
                result.Variables[2 * i],
                result.Variables[2 * i + 1]
            );
        }

        return true;

        double[] Residuals(double[] candidate)
        {
            var residuals = new List<double>(componentConstraints.Count + keys.Length * 2);
            
            // Hard constraint, large optimization weight
            foreach (var constraint in componentConstraints)
            {
                int fronIdx = indexByKey[constraint.FromKey] * 2;
                int toIdx = indexByKey[constraint.ToKey] * 2;

                double dx = candidate[toIdx] - candidate[fronIdx];
                double dy = candidate[toIdx + 1] - candidate[fronIdx + 1];

                double currentLength = Math.Sqrt(dx * dx + dy * dy);

                residuals.Add(ConstraintWeight * (currentLength - constraint.Length));
            }

            for (var i = 0; i < keys.Length; i++)
            {
                double x = candidate[2 * i];
                double y = candidate[2 * i + 1];

                if (i == targetIndex)
                {
                    // Prefer the requested position
                    residuals.Add(x - requestedPosition.X);
                    residuals.Add(y - requestedPosition.Y);
                }
                else
                {
                    // Prefer to keep other vertices near their previous positions
                    var original = verticesByKey[keys[i]].Position;

                    residuals.Add(PreservationWeight * (x - original.X));
                    residuals.Add(PreservationWeight * (y - original.Y));
                }
            }

            return [.. residuals];
        }
    }

    private List<ResolvedFixedConstraint>? ResolveFixedConstraints()
    {
        var resolved = new List<ResolvedFixedConstraint>();

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

            resolved.Add(new ResolvedFixedConstraint(
                new VertexKey(polygon.Id, from.Id),
                new VertexKey(polygon.Id, to.Id),
                constraint.Length)
            );
        }

        return resolved;
    }

    private static HashSet<VertexKey> FindConnectedVertices(
        VertexKey start,
        IReadOnlyList<ResolvedFixedConstraint> constraints)
    {
        var visited = new HashSet<VertexKey> { start };
        var queue = new Queue<VertexKey>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var constraint in constraints)
            {
                VertexKey? neighbor = null;

                if (constraint.FromKey == current)
                    neighbor = constraint.ToKey;
                else if (constraint.ToKey == current)
                    neighbor = constraint.FromKey;

                if (neighbor is not { } next || !visited.Add(next))
                    continue;

                queue.Enqueue(next);
            }
        }

        return visited;
    }

    private Vertex? FindVertex(VertexKey key)
    {
        return scene.Polygons
            .FirstOrDefault(p => p.Id == key.PolygonId)?
            .Vertices.FirstOrDefault(v => v.Id == key.VertexId);
    }

    private readonly record struct VertexKey(
        Guid PolygonId,
        Guid VertexId
    );

    private sealed record ResolvedFixedConstraint(
        VertexKey FromKey,
        VertexKey ToKey,
        double Length
    );
}
