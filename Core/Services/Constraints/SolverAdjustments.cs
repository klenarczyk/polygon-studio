using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services.Constraints;

internal static class SolverAdjustments
{
    // --- Vertices & Control Points ---
    
    internal static void MoveAdjacentControlPoint(VertexKey vertexKey, Point2D delta, SolverState state)
    {
        if (!state.VertexAdjacentBeziers.TryGetValue(vertexKey, out var adjacentList))
            return;
        
        foreach ((var edgeKey, bool isFromVertex) in adjacentList)
        {
            var (cp1, cp2) = state.Beziers[edgeKey];
            
            if (isFromVertex)
                state.Beziers[edgeKey] = (cp1 + delta, cp2);
            else
                state.Beziers[edgeKey] = (cp1, cp2 + delta);
        }
    }

    private static double MoveVertexWithControls(VertexKey vertexKey, Point2D newPosition, SolverState state)
    {
        var oldPos = state.Vertices[vertexKey];
        double distSq = MathHelper.DistanceSquared(oldPos, newPosition);
        if (distSq < 1e-18)
            return 0.0;

        var delta = new Point2D(newPosition.X - oldPos.X, newPosition.Y - oldPos.Y);
        state.Vertices[vertexKey] = newPosition;

        MoveAdjacentControlPoint(vertexKey, delta, state);
        return distSq;
    }
    
    // --- Constraints ---
    
    internal static double AdjustFixedLength(
        ResolvedFixedConstraint constraint,
        SolverState state,
        Dictionary<VertexKey, int> ranks,
        HashSet<VertexKey> lockedVertices)
    {
        var pointA = state.Vertices[constraint.From];
        var pointB = state.Vertices[constraint.To];

        double dx = pointB.X - pointA.X;
        double dy = pointB.Y - pointA.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);

        if (len < 1e-12)
            return 0.0;

        double diff = len - constraint.EdgeLength;
        if (Math.Abs(diff) < 1e-9)
            return 0.0;

        (double weightA, double weightB) = VertexRanking.GetVertexWeights(
            constraint.From, constraint.To,
            ranks, lockedVertices
        );

        if (weightA + weightB == 0.0)
            return 0.0;

        double ux = dx / len;
        double uy = dy / len;

        var newA = new Point2D(pointA.X + weightA * diff * ux, pointA.Y + weightA * diff * uy);
        var newB = new Point2D(pointB.X - weightB * diff * ux, pointB.Y - weightB * diff * uy);

        return MoveVertexWithControls(constraint.From, newA, state) +
               MoveVertexWithControls(constraint.To, newB, state);
    }
    
    internal static double AdjustPerpendicular(
        ResolvedPerpendicularConstraint constraint,
        SolverState state,
        Dictionary<VertexKey, int> ranks,
        HashSet<VertexKey> lockedVertices)
    {
        var a = state.Vertices[constraint.From1];
        var b = state.Vertices[constraint.To1];
        var c = state.Vertices[constraint.From2];
        var d = state.Vertices[constraint.To2];

        var v1 = new Point2D(b.X - a.X, b.Y - a.Y);
        var v2 = new Point2D(d.X - c.X, d.Y - c.Y);

        double len1 = MathHelper.Length(v1);
        double len2 = MathHelper.Length(v2);

        if (len1 < 1e-12 || len2 < 1e-12)
            return 0.0;

        double dot = MathHelper.Dot(v1, v2) / (len1 * len2);
        if (Math.Abs(dot) < 1e-9)
            return 0.0;

        int rankEdge1 = Math.Min(
            VertexRanking.GetRank(ranks, constraint.From1), 
            VertexRanking.GetRank(ranks, constraint.To1)
        );
        
        int rankEdge2 = Math.Min(
            VertexRanking.GetRank(ranks, constraint.From2), 
            VertexRanking.GetRank(ranks, constraint.To2)
        );

        bool isEdge1Locked = lockedVertices.Contains(constraint.From1) && 
                                lockedVertices.Contains(constraint.To1);
        
        bool isEdge2Locked = lockedVertices.Contains(constraint.From2) && 
                             lockedVertices.Contains(constraint.To2);

        if (isEdge1Locked && isEdge2Locked)
            return 0.0;
        
        if (isEdge1Locked || (!isEdge2Locked && rankEdge1 <= rankEdge2))
        {
            return RotateToPerpendicular(
                constraint.From2, constraint.To2, v1,
                constraint.From1, constraint.To1, 
                state, ranks, lockedVertices
            );
        }

        return RotateToPerpendicular(
            constraint.From1, constraint.To1, v2, 
            constraint.From1, constraint.To1,
            state, ranks, lockedVertices
        );
    }

    private static double RotateToPerpendicular(
        VertexKey keyC, VertexKey keyD,
        Point2D referenceVec,
        VertexKey refKey1, VertexKey refKey2,
        SolverState state,
        Dictionary<VertexKey, int> ranks,
        HashSet<VertexKey> lockedVertices)
    {
        var c = state.Vertices[keyC];
        var d = state.Vertices[keyD];
        
        var currentVec = new Point2D(d.X - c.X, d.Y - c.Y);
        
        double len = MathHelper.Length(currentVec);
        double refLen = MathHelper.Length(referenceVec);

        if (len < 1e-12 || refLen < 1e-12)
            return 0.0;

        // Choose (-y, x) or (y, -x)
        var perp1 = new Point2D(-referenceVec.Y / refLen, referenceVec.X / refLen);
        var perp2 = new Point2D(referenceVec.Y / refLen, -referenceVec.X / refLen);
        
        var targetDir = MathHelper.Dot(currentVec, perp1) >= 0 ? perp1 : perp2;
        var targetVec = new Point2D(targetDir.X * len, targetDir.Y * len);

        // Choose the center of rotation
        (double weightC, double weightD) = VertexRanking.GetVertexWeights(keyC, keyD, ranks, lockedVertices);
        
        if (keyC == refKey1 || keyC == refKey2)
        {
            weightC = 0.0;
            weightD = 1.0;
        }
        else if (keyD == refKey1 || keyD == refKey2)
        {
            weightC = 1.0;
            weightD = 0.0;
        }
        
        if (weightC + weightD == 0.0) 
            return 0.0;
        
        if (weightC == 0.0)
        {
            var newPos = new Point2D(c.X + targetVec.X, c.Y + targetVec.Y);
            return MoveVertexWithControls(keyD, newPos, state);
        }

        if (weightD == 0.0)
        {
            var newPos = new Point2D(d.X - targetVec.X, d.Y - targetVec.Y);
            return MoveVertexWithControls(keyC, newPos, state);
        }
        
        var mid = new Point2D(0.5 * (c.X + d.X), 0.5 * (c.Y + d.Y));
        var newC = new Point2D(mid.X - 0.5 * targetVec.X, mid.Y - 0.5 * targetVec.Y);
        var newD = new Point2D(mid.X + 0.5 * targetVec.X, mid.Y + 0.5 * targetVec.Y);

        return MoveVertexWithControls(keyC, newC, state) + MoveVertexWithControls(keyD, newD, state);
    }
    
    // --- Continuities ---
    
    internal static void UpdateArcBulge(ResolvedContinuity continuity, SolverState state)
    {
        if (continuity.Type != ContinuityType.G1) return;

        bool incomingIsArc = continuity.Incoming.Geometry is CircularArcGeometry;
        bool outgoingIsArc = continuity.Outgoing.Geometry is CircularArcGeometry;
        
        if (!incomingIsArc && !outgoingIsArc)
            return;
        
        var arc = outgoingIsArc ? continuity.Outgoing : continuity.Incoming;
        var tangentEdge = outgoingIsArc ? continuity.Incoming : continuity.Outgoing;

        var polygon = continuity.Polygon;
        var polygonId = polygon.Id;

        var tangent = MathHelper.EvaluateDerivative(state, polygon, tangentEdge, outgoingIsArc);
        if (MathHelper.Length(tangent) <= 1e-12)
            return;

        var start = state.Vertices[new VertexKey(polygonId, arc.FromVertexId)];
        var end = state.Vertices[new VertexKey(polygonId, arc.ToVertexId)];

        double bulge = MathHelper.CalculateBulgeFromTangent(start, end, tangent, outgoingIsArc);
        state.Arcs[new EdgeKey(polygonId, arc.Id)] = bulge;
    }

    internal static double AdjustBezierContinuity(
        ResolvedContinuity continuity, SolverState state,
        HashSet<VertexKey> lockedVertices,
        HashSet<(EdgeKey Edge, int Index)> lockedControlPoints)
    {
        if (continuity.Type == ContinuityType.G0)
            return 0.0;

        var polyId = continuity.Polygon.Id;
        var vertexKey = new VertexKey(polyId, continuity.VertexId);
        var inKey = new EdgeKey(polyId, continuity.Incoming.Id);
        var outKey = new EdgeKey(polyId, continuity.Outgoing.Id);
        
        return (continuity.Incoming.Geometry, continuity.Outgoing.Geometry) switch
        {
            (CubicBezierGeometry, CubicBezierGeometry) => AlignBezierHandles(
                vertexKey, inKey, outKey, 
                continuity.Type, state, lockedControlPoints
            ),

            (LineGeometry, CubicBezierGeometry) => AlignLineAndBezier(
                new VertexKey(polyId, continuity.Incoming.FromVertexId), 
                vertexKey, outKey, true, 
                continuity.Type, state, 
                lockedVertices, lockedControlPoints
            ),

            (CubicBezierGeometry, LineGeometry) => AlignLineAndBezier(
                new VertexKey(polyId, continuity.Outgoing.ToVertexId), 
                vertexKey, inKey, false, 
                continuity.Type, state, 
                lockedVertices, lockedControlPoints
            ),

            _ => 0.0
        };
    }

    private static double AlignBezierHandles(
        VertexKey vertexKey, EdgeKey inKey, EdgeKey outKey,
        ContinuityType type, SolverState state,
        HashSet<(EdgeKey Edge, int Index)> lockedCPs)
    {
        var vertex = state.Vertices[vertexKey];
        var incoming = state.Beziers[inKey];
        var outgoing = state.Beziers[outKey];

        bool cp2Locked = lockedCPs.Contains((inKey, 1));
        bool cp1Locked = lockedCPs.Contains((outKey, 0));

        if (cp1Locked && cp2Locked) return 0.0;

        var inVec = vertex - incoming.Cp2;
        var outVec = outgoing.Cp1 - vertex;
        
        double inLen = MathHelper.Length(inVec);
        double outLen = MathHelper.Length(outVec);
        
        bool inDegenerate = inLen < 1e-12;
        bool outDegenerate = outLen < 1e-12;
        
        if (cp2Locked)
        {
            if (inDegenerate) return 0.0;
            
            var newCp1 = vertex + inVec / inLen * (type == ContinuityType.C1 ? inLen : outLen);
            state.Beziers[outKey] = (newCp1, outgoing.Cp2);
            return MathHelper.DistanceSquared(outgoing.Cp1, newCp1);
        }

        if (cp1Locked)
        {
            if (outDegenerate) return 0.0;
            
            var newCp2 = vertex - outVec / outLen * (type == ContinuityType.C1 ? outLen : inLen);
            state.Beziers[inKey] = (incoming.Cp1, newCp2);
            return MathHelper.DistanceSquared(incoming.Cp2, newCp2);
        }
        
        if (inDegenerate && outDegenerate) 
            return 0.0;

        Point2D direction;
        if (inDegenerate)
        {
            direction = outVec / outLen;
        }
        else if (outDegenerate)
        {
            direction = inVec / inLen;
        }
        else
        {
            var sum = inVec / inLen + outVec / outLen;
            double sumLen = MathHelper.Length(sum);
            direction = sumLen > 1e-12 ? sum / sumLen : inVec / inLen;
        }
        
        double targetInLen = type == ContinuityType.C1 
            ? (inDegenerate ? outLen : outDegenerate ? inLen : (inLen + outLen) / 2.0)
            : (inDegenerate ? outLen : inLen);

        double targetOutLen = type == ContinuityType.C1 
            ? targetInLen 
            : (outDegenerate ? inLen : outLen);

        var finalCp2 = vertex - direction * targetInLen;
        var finalCp1 = vertex + direction * targetOutLen;

        state.Beziers[inKey] = (incoming.Cp1, finalCp2);
        state.Beziers[outKey] = (finalCp1, outgoing.Cp2);

        return Math.Max(
            MathHelper.DistanceSquared(incoming.Cp2, finalCp2),
            MathHelper.DistanceSquared(outgoing.Cp1, finalCp1)
        );
    }

    private static double AlignLineAndBezier(
        VertexKey lineKey, VertexKey sharedKey, EdgeKey bezierKey,
        bool isOutgoing, ContinuityType type, SolverState state,
        HashSet<VertexKey> lockedVertices,
        HashSet<(EdgeKey Edge, int Index)> lockedCPs)
    {
        var vertex = state.Vertices[sharedKey];
        var lineOther = state.Vertices[lineKey];
        var bezier = state.Beziers[bezierKey];

        int cpIndex = isOutgoing ? 0 : 1;
        var cp = isOutgoing ? bezier.Cp1 : bezier.Cp2;
        
        var cpVec = isOutgoing ? cp - vertex : vertex - cp;
        var lineVec = isOutgoing ? vertex - lineOther : lineOther - vertex;

        if (lockedCPs.Contains((bezierKey, cpIndex)))
        {
            double cpLen = MathHelper.Length(cpVec);
            if (cpLen < 1e-12 || lockedVertices.Contains(lineKey)) 
                return 0.0;

            var dir = cpVec / cpLen;
            double lineLen = MathHelper.Distance(lineOther, vertex);
            bool fixedLen = state.FixedConstraints.Any(fc => 
                (fc.From == sharedKey && fc.To == lineKey) || (fc.From == lineKey && fc.To == sharedKey)
            );

            if (type == ContinuityType.C1 && fixedLen)
            {
                var adjustedCp = vertex + (isOutgoing ? dir : -dir) * (lineLen / 3.0);
                state.Beziers[bezierKey] = isOutgoing ? (adjustedCp, bezier.Cp2) : (bezier.Cp1, adjustedCp);
                return MathHelper.DistanceSquared(cp, adjustedCp);
            }

            double targetLineLen = type == ContinuityType.C1 ? 3.0 * cpLen : lineLen;
            var newLineOther = vertex + (isOutgoing ? -dir : dir) * targetLineLen;
            return MoveVertexWithControls(lineKey, newLineOther, state);
        }
        else
        {
            double lineLen = MathHelper.Length(lineVec);
            if (lineLen < 1e-12) return 0.0;

            var dir = lineVec / lineLen;
            double currentCpLen = MathHelper.Distance(cp, vertex);
            double targetCpLen = type == ContinuityType.C1 ? lineLen / 3.0 : currentCpLen;

            var newCp = vertex + (isOutgoing ? dir : -dir) * targetCpLen;
            state.Beziers[bezierKey] = isOutgoing ? (newCp, bezier.Cp2) : (bezier.Cp1, newCp);
            return MathHelper.DistanceSquared(cp, newCp);
        }
    }
}