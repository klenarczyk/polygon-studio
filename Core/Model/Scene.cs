using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Model;

public class Scene
{
    private readonly List<Polygon> _polygons = [];
    public IReadOnlyList<Polygon> Polygons => _polygons;
    
    private readonly List<IConstraint> _constraints = [];
    public IReadOnlyList<IConstraint> Constraints => _constraints;

    public Scene()
    {
        Initialize();
    }
    
    public bool TryAddConstraint(IConstraint constraint)
    {
        if (_constraints.Any(c => c.Id == constraint.Id))
            return false;

        bool isValid = constraint switch
        {
            FixedLengthConstraint fixedLength => IsValidFixedLengthConstraint(fixedLength),

            PerpendicularConstraint perpendicular => IsValidPerpendicularConstraint(perpendicular),

            _ => false
        };

        if (!isValid)
            return false;
        
        _constraints.Add(constraint);
        return true;
    }

    public bool RemoveConstraint(Guid constraintId)
    {
        var constraint = _constraints.FirstOrDefault(c => c.Id == constraintId);
        return constraint is not null && _constraints.Remove(constraint);
    }
    
    public void RemoveConstraintsForEdge(Guid polygonId, Guid edgeId)
    {
        _constraints.RemoveAll(c => c switch
        {
            FixedLengthConstraint fixedLength =>
                fixedLength.PolygonId == polygonId && fixedLength.EdgeId == edgeId,

            PerpendicularConstraint perpendicular =>
                (perpendicular.PolygonId1 == polygonId && perpendicular.EdgeId1 == edgeId) ||
                (perpendicular.PolygonId2 == polygonId && perpendicular.EdgeId2 == edgeId),

            _ => false
        });
    }
    
    public void Reset()
    {
        _polygons.Clear();
        _constraints.Clear();
        
        Initialize();
    }
    
    internal void RestoreContents(IReadOnlyList<Polygon> polygons, IReadOnlyList<IConstraint> constraints)
    {
        _polygons.Clear();
        _polygons.AddRange(polygons);

        _constraints.Clear();
        _constraints.AddRange(constraints);
    }
    
    // ---

    private void Initialize()
    {
        var polygon1 = new Polygon(
        [
            new Point2D(-100.0, 0.0),
            new Point2D(-55.0, 50.0),
            new Point2D(-10.0, 0.0)
        ]);
        
        var polygon2 = new Polygon(
        [
            new Point2D(10.0, 50.0),
            new Point2D(55.0, 0.0),
            new Point2D(100.0, 50.0)
        ]);
        
        _polygons.Add(polygon1);
        _polygons.Add(polygon2);
    }
    
    private bool IsValidFixedLengthConstraint(FixedLengthConstraint constraint)
    {
        var polygon = _polygons.FirstOrDefault(p => p.Id == constraint.PolygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId);
        
        if (edge == null)
            return false;

        return !_constraints
            .OfType<FixedLengthConstraint>()
            .Any(existing =>
                existing.PolygonId == constraint.PolygonId &&
                existing.EdgeId == constraint.EdgeId
            );
    }

    private bool IsValidPerpendicularConstraint(PerpendicularConstraint constraint)
    {
        if (constraint.PolygonId1 == constraint.PolygonId2 && constraint.EdgeId1 == constraint.EdgeId2)
            return false;

        var polygon1 = _polygons.FirstOrDefault(p => p.Id == constraint.PolygonId1);
        var polygon2 = _polygons.FirstOrDefault(p => p.Id == constraint.PolygonId2);
        
        var edge1 = polygon1?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId1);
        var edge2 = polygon2?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId2);
        
        if (edge1 == null ||  edge2 == null)
            return false;
        
        if (edge1.Geometry is not LineGeometry || edge2.Geometry is not LineGeometry)
            return false;

        return !_constraints
            .OfType<PerpendicularConstraint>()
            .Any(c => 
                c.EdgeId1 == edge1.Id ||  c.EdgeId1 == edge2.Id || 
                c.EdgeId2 == edge1.Id || c.EdgeId2 == edge2.Id
            );
    }
}