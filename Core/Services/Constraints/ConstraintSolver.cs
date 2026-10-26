using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services.Constraints;

internal sealed class ConstraintSolver(Scene scene)
{
    private const int MaxIterations = 50;
    private const double ConvergenceEpsilon = 1e-6;
    private const double Tolerance = 1e-3;

    internal bool TrySolveCurrentScene()
    {
        var state = SolverState.Capture(scene);
        if (state is null) return false;

        if (!Relax(state, null, [], []))
            return false;

        Commit(state);
        return true;
    }
    
    internal bool TryMoveTarget(MoveTarget target, Point2D requestedPosition)
    {
        var state = SolverState.Capture(scene);
        if (state is null) return false;

        VertexKey? bfsSeed;
        var lockedVertices = new HashSet<VertexKey>();
        var lockedControlPoints = new HashSet<(EdgeKey Edge, int Index)>();

        if (target.Kind == MoveTargetKind.Vertex)
        {
            var vertexKey = new VertexKey(target.PolygonId, target.ElementId);
            if (!state.Vertices.TryGetValue(vertexKey, out var oldPos))
                return false;

            var delta = new Point2D(requestedPosition.X - oldPos.X, requestedPosition.Y - oldPos.Y);
            state.Vertices[vertexKey] = requestedPosition;
            lockedVertices.Add(vertexKey);
            bfsSeed = vertexKey;

            SolverAdjustments.MoveAdjacentControlPoint(vertexKey, delta, state);
        }
        else if (target.Kind == MoveTargetKind.BezierControlPoint)
        {
            if (target.ControlPointIndex is not (0 or 1))
                return false;

            var edgeKey = new EdgeKey(target.PolygonId, target.ElementId);
            if (!state.Beziers.TryGetValue(edgeKey, out var cp))
                return false;

            int cpIdx = target.ControlPointIndex;
            state.Beziers[edgeKey] = cpIdx == 0
                ? (requestedPosition, cp.Cp2)
                : (cp.Cp1, requestedPosition);

            lockedControlPoints.Add((edgeKey, cpIdx));
            
            var polygon = scene.Polygons.First(p => p.Id == target.PolygonId);
            var edge = polygon.Edges.First(e => e.Id == target.ElementId);

            var from = new VertexKey(polygon.Id, edge.FromVertexId);
            var to = new VertexKey(polygon.Id, edge.ToVertexId);

            lockedVertices.Add(from);
            lockedVertices.Add(to);

            var adjacentVertex = cpIdx == 0 ? from : to;
            bfsSeed = adjacentVertex;
        }
        else return false;
        
        if (!Relax(state, bfsSeed, lockedVertices, lockedControlPoints))
        {
            // Try locking only the closer endpoint
            if (target.Kind == MoveTargetKind.BezierControlPoint && lockedVertices.Count > 1)
            {
                lockedVertices.Clear();
                lockedVertices.Add(bfsSeed.Value);
            
                if (!Relax(state, bfsSeed, lockedVertices, lockedControlPoints))
                    return false;
            }
            else
                return false;
        }

        Commit(state);
        return true;
    }

    // ---
    
    private bool Relax(
        SolverState state,
        VertexKey? bfsSeed,
        HashSet<VertexKey> lockedVertices,
        HashSet<(EdgeKey Edge, int Index)> lockedControlPoints)
    {
        var ranks = VertexRanking.ComputeVertexRanks(scene, state, bfsSeed);
        
        var orderedFixed = state.FixedConstraints
            .OrderBy(c => Math.Min(
                VertexRanking.GetRank(ranks, c.From), 
                VertexRanking.GetRank(ranks, c.To)))
            .ToList();

        var orderedPerpendicular = state.PerpendicularConstraints
            .OrderBy(c => Math.Min(
                Math.Min(VertexRanking.GetRank(ranks, c.From1), VertexRanking.GetRank(ranks, c.To1)),
                Math.Min(VertexRanking.GetRank(ranks, c.From2), VertexRanking.GetRank(ranks, c.To2))))
            .ToList();

        var orderedContinuities = state.Continuities
            .OrderBy(c => VertexRanking.GetRank(ranks, new VertexKey(c.Polygon.Id, c.VertexId)))
            .ToList();

        for (var i = 0; i < MaxIterations; i++)
        {
            var maxDeltaSquared = 0.0;
            
            foreach (var fc in orderedFixed)
            {
                double d = SolverAdjustments.AdjustFixedLength(fc, state, ranks, lockedVertices);
                if (d > maxDeltaSquared) maxDeltaSquared = d;
            }
            
            foreach (var pc in orderedPerpendicular)
            {
                double d = SolverAdjustments.AdjustPerpendicular(pc, state, ranks, lockedVertices);
                if (d > maxDeltaSquared) maxDeltaSquared = d;
            }
            
            foreach (var continuity in orderedContinuities)
            {
                double d = SolverAdjustments.AdjustBezierContinuity(
                    continuity, state, 
                    lockedVertices, lockedControlPoints
                );
                if (d > maxDeltaSquared) maxDeltaSquared = d;
            }
            
            foreach (var continuity in orderedContinuities)
                SolverAdjustments.UpdateArcBulge(continuity, state);

            if (maxDeltaSquared < ConvergenceEpsilon * ConvergenceEpsilon)
                break;
        }

        return ValidateState(state);
    }

    // ---
    
    private static bool ValidateState(SolverState state)
    {
        foreach (var fc in state.FixedConstraints)
        {
            var pointA = state.Vertices[fc.From];
            var pointB = state.Vertices[fc.To];
            
            double len = MathHelper.Distance(pointA, pointB);
            double tol = Tolerance * Math.Max(1.0, fc.EdgeLength);

            if (Math.Abs(len - fc.EdgeLength) > tol)
                return false;
        }

        foreach (var pc in state.PerpendicularConstraints)
        {
            var a = state.Vertices[pc.From1];
            var b = state.Vertices[pc.To1];
            var c = state.Vertices[pc.From2];
            var d = state.Vertices[pc.To2];

            var v1 = new Point2D(b.X - a.X, b.Y - a.Y);
            var v2 = new Point2D(d.X - c.X, d.Y - c.Y);

            double len1 = MathHelper.Length(v1);
            double len2 = MathHelper.Length(v2);

            if (len1 < 1e-9 || len2 < 1e-9)
                return false;

            double normalizedDot = Math.Abs(MathHelper.Dot(v1, v2)) / (len1 * len2);
            if (normalizedDot > Tolerance)
                return false;
        }

        foreach (var continuity in state.Continuities)
        {
            if (continuity.Type == ContinuityType.G0) continue;
            
            var a = MathHelper.EvaluateDerivative(state, continuity.Polygon, continuity.Incoming, true);
            var b = MathHelper.EvaluateDerivative(state, continuity.Polygon, continuity.Outgoing, false);

            double lenA = MathHelper.Length(a);
            double lenB = MathHelper.Length(b);

            if (lenA < 1e-9 || lenB < 1e-9)
                continue;

            if (continuity.Type == ContinuityType.G1)
            {
                double normalizedCross = Math.Abs(MathHelper.Cross(a, b)) / (lenA * lenB);
                if (normalizedCross > Tolerance || MathHelper.Dot(a, b) <= 0)
                    return false;
            }
            else if (continuity.Type == ContinuityType.C1)
            {
                double error = MathHelper.Distance(a, b);
                if (error > Tolerance * Math.Max(1.0, Math.Max(lenA, lenB)))
                    return false;
            }
        }

        return true;
    }

    private void Commit(SolverState state)
    {
        foreach (var polygon in scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
                vertex.Position = state.Vertices[new VertexKey(polygon.Id, vertex.Id)];

            foreach (var edge in polygon.Edges)
            {
                var key = new EdgeKey(polygon.Id, edge.Id);

                switch (edge.Geometry)
                {
                    case CubicBezierGeometry:
                        var (cp1, cp2) = state.Beziers[key];
                        edge.Geometry = new CubicBezierGeometry(cp1, cp2);
                        break;
                    
                    case CircularArcGeometry:
                        edge.Geometry = new CircularArcGeometry(state.Arcs[key]);
                        break;
                }
            }
        }
    }
}