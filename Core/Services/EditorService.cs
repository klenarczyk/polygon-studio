using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services.Constraints;

namespace PolygonStudio.Core.Services;

public sealed class EditorService(Scene scene)
{
    public Scene Scene { get; } = scene;
    private readonly ConstraintSolver _solver = new(scene);

    public bool MoveVertex(Guid polygonId, Guid vertexId, Point2D newPosition) =>
        _solver.TryMoveTarget(MoveTarget.Vertex(polygonId, vertexId), newPosition);

    public bool SolveCurrentScene() => _solver.TrySolveCurrentScene();
    
    public bool MoveBezierControlPoint(
        Guid polygonId,
        Guid edgeId,
        int controlPointIndex,
        Point2D newPosition)
    {
        if (controlPointIndex is not (0 or 1))
            return false;

        return _solver.TryMoveTarget(
            MoveTarget.BezierControlPoint(polygonId, edgeId, controlPointIndex), 
            newPosition
        );
    }

    public bool SplitEdge(Guid polygonId, Guid edgeId)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);

        if (polygon is null || edge is null) return false;
        
        var start = polygon.Vertices.First(v => v.Id == edge.FromVertexId).Position;
        var end = polygon.Vertices.First(v => v.Id == edge.ToVertexId).Position;
        
        var newVertexPosition = new Point2D((end.X + start.X) / 2, (end.Y + start.Y) / 2);
        var newVertex = new Vertex(newVertexPosition);
        
        polygon.AddVertex(newVertex);

        var edge1 = new Edge(edge.FromVertexId, newVertex.Id);
        var edge2 = new Edge(newVertex.Id, edge.ToVertexId);
        
        polygon.AddEdge(edge1);
        polygon.AddEdge(edge2);
        
        Scene.RemoveConstraintsForEdge(polygonId, edge.Id);
        
        polygon.RemoveEdge(edge.Id);
  
        ContinuityHelper.ResetInvalid(Scene);
        
        return SolveCurrentScene();
    }
    
    public bool SetEdgeGeometry(Guid polygonId, Guid edgeId, Type geometryType)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);

        if (polygon is null || edge is null) return false;

        if (edge.Geometry.GetType() == geometryType)
            return true;

        var start = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
        var end = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);

        if (start is null || end is null)
            return false;
        
        // - Temporary solution -
        
        var previousGeometries = polygon.Edges.ToDictionary(
            e => e.Id, e => e.Geometry);

        var previousContinuities = polygon.Vertices.ToDictionary(
            v => v.Id, v => v.Continuity);

        var removedConstraints = Scene.Constraints
            .Where(c => 
                (c is FixedLengthConstraint fixedLength && fixedLength.EdgeId == edge.Id) || 
                (c is PerpendicularConstraint perp && (perp.EdgeId1 == edgeId || perp.EdgeId2 == edgeId)))
            .ToList();
        
        // - -
        
        Scene.RemoveConstraintsForEdge(polygonId, edgeId);
        
        edge.Geometry = EdgeGeometryFactory.CreateDefault(start.Position, end.Position, geometryType);
        
        ContinuityHelper.UpdateForGeometryChange(polygon, start.Id);
        ContinuityHelper.UpdateForGeometryChange(polygon, end.Id);
        
        ContinuityHelper.ResetInvalid(Scene);
        ContinuityHelper.NormalizeArcBulges(polygon);
        
        if (SolveCurrentScene())
            return true;

        // - Temporary solution -
        foreach (var polygonEdge in polygon.Edges)
        {
            if (previousGeometries.TryGetValue(polygonEdge.Id, out var geometry))
                polygonEdge.Geometry = geometry;
        }

        foreach (var vertex in polygon.Vertices)
        {
            if (previousContinuities.TryGetValue(vertex.Id, out var continuity))
                vertex.Continuity = continuity;
        }
        
        foreach (var constraint in removedConstraints)
            Scene.TryAddConstraint(constraint);

        // - -
        
        return false;
    }
    
    public bool RemoveVertex(Guid polygonId, Guid vertexId)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var vertex = polygon?.Vertices.FirstOrDefault(v => v.Id == vertexId);

        if (polygon is null || vertex is null) 
            return false;

        if (polygon.Vertices.Count <= 3)
            return false;
        
        var edge1 = polygon.Edges.First(e => e.ToVertexId == vertex.Id);
        var edge2 = polygon.Edges.First(e => e.FromVertexId == vertex.Id);
        
        var newEdge = new Edge(edge1.FromVertexId, edge2.ToVertexId);
        
        polygon.AddEdge(newEdge);
        
        Scene.RemoveConstraintsForEdge(polygonId, edge1.Id);
        Scene.RemoveConstraintsForEdge(polygonId, edge2.Id);
        
        polygon.RemoveEdge(edge1.Id);
        polygon.RemoveEdge(edge2.Id);
        polygon.RemoveVertex(vertexId);
        
        ContinuityHelper.ResetInvalid(Scene);
        
        return SolveCurrentScene();
    }
    
    public bool MovePolygon(Guid polygonId, Point2D delta)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);

        if (polygon is null)
            return false;

        foreach (var vertex in polygon.Vertices)
        {
            vertex.Position = new Point2D(
                vertex.Position.X + delta.X,
                vertex.Position.Y + delta.Y
            );
        }

        foreach (var edge in polygon.Edges)
        {
            if (edge.Geometry is not CubicBezierGeometry bezier) continue;
            
            var control1 = new Point2D(
                bezier.ControlPoint1.X + delta.X,
                bezier.ControlPoint1.Y + delta.Y
            );

            var control2 = new Point2D(
                bezier.ControlPoint2.X + delta.X,
                bezier.ControlPoint2.Y + delta.Y
            );
                
            edge.Geometry = new CubicBezierGeometry(control1, control2);
        }

        return true;
    }
    
    public bool AddFixedEdgeLengthConstraint(Guid polygonId, Guid edgeId, double length)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);
        
        if (polygon is null || edge is null) return false;

        var constraint = new FixedLengthConstraint(polygonId, edgeId, length);

        if (!Scene.TryAddConstraint(constraint))
            return false;
        
        if (SolveCurrentScene())
            return true;

        Scene.RemoveConstraint(constraint.Id);
        return false;
    }

    public bool UpdateFixedEdgeLengthConstraint(FixedLengthConstraint fixedLength, double length)
    {
        if (length <= 0)
            return false;

        double previousLength = fixedLength.Length;
        fixedLength.Length = length;

        if (SolveCurrentScene())
            return true;

        fixedLength.Length = previousLength;
        return false;
    }
    
    public bool AddPerpendicularConstraint(
        Guid polygonId1, Guid edgeId1,
        Guid polygonId2, Guid edgeId2)
    {
        var polygon1 = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId1);
        var polygon2 = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId2);

        var edge1 = polygon1?.Edges.FirstOrDefault(e => e.Id == edgeId1);
        var edge2 = polygon2?.Edges.FirstOrDefault(e => e.Id == edgeId2);

        if (edge1 is null || edge2 is null ||
            edge1.Geometry is not LineGeometry ||
            edge2.Geometry is not LineGeometry)
        {
            return false;
        }

        if (polygonId1 == polygonId2 && edgeId1 == edgeId2)
            return false;

        var constraint = new PerpendicularConstraint(polygonId1, edgeId1, polygonId2, edgeId2);

        if (!Scene.TryAddConstraint(constraint))
            return false;

        if (SolveCurrentScene())
            return true;

        Scene.RemoveConstraint(constraint.Id);
        return false;
    }
    
    public bool RemoveConstraint(Guid constraintId) =>
        Scene.RemoveConstraint(constraintId);
    
    public bool SetVertexContinuity(Guid polygonId, Guid vertexId, ContinuityType type)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var vertex = polygon?.Vertices.FirstOrDefault(v => v.Id == vertexId);

        if (polygon is null || vertex is null)
            return false;

        if (!ContinuityHelper.CanApply(polygon, vertexId, type))
            return false;

        var previousContinuity = vertex.Continuity;
        var previousGeometries = polygon.Edges
            .ToDictionary(e => e.Id, e => e.Geometry);

        vertex.Continuity = type;
        ContinuityHelper.NormalizeArcBulges(polygon);

        if (SolveCurrentScene())
            return true;

        vertex.Continuity = previousContinuity;

        foreach (var edge in polygon.Edges)
        {
            if (previousGeometries.TryGetValue(edge.Id, out var geometry))
                edge.Geometry = geometry;
        }

        return false;
    }

    public ContinuityType? GetVertexContinuity(Guid polygonId, Guid vertexId)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var vertex = polygon?.Vertices.FirstOrDefault(v => v.Id == vertexId);

        return vertex?.Continuity;
    }
}