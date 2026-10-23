using System.Drawing;
using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Model;

public class Scene
{
    private readonly List<Polygon> _polygons = [];
    public IReadOnlyList<Polygon> Polygons => _polygons;
    
    private readonly List<Constraint> _constraints = [];
    public IReadOnlyList<Constraint> Constraints => _constraints;

    public Scene()
    {
        Initialize();
    }
    
    public bool TryAddConstraint(Constraint constraint)
    {
        if (_constraints.Any(c => c.Id == constraint.Id))
            return false;

        bool isValid = constraint switch
        {
            FixedEdgeLengthConstraint fixedLength => IsValidFixedLengthConstraint(fixedLength),

            PerpendicularEdgesConstraint perpendicular => IsValidPerpendicularConstraint(perpendicular),

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
            FixedEdgeLengthConstraint fixedLength =>
                fixedLength.PolygonId == polygonId && fixedLength.EdgeId == edgeId,

            PerpendicularEdgesConstraint perpendicular =>
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
        
        // polygon1.Edges[0].Geometry = new CubicBezierGeometry(
        //     new Point2D(-100.0, 25.0),
        //     new Point2D(-55.0, 25.0)
        // );
        //
        // polygon2.Edges[0].Geometry = CircularArcGeometry.CreateSemicircle();
        //
        // var constraint = new FixedEdgeLengthConstraint(polygon1.Id, polygon1.Edges[2].Id, 90.0);
        
        _polygons.Add(polygon1);
        _polygons.Add(polygon2);
        
        // _constraints.Add(constraint);
    }
    
    private bool IsValidFixedLengthConstraint(FixedEdgeLengthConstraint constraint)
    {
        var polygon = _polygons.FirstOrDefault(p => p.Id == constraint.PolygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId);
        
        if (edge == null)
            return false;

        return !_constraints
            .OfType<FixedEdgeLengthConstraint>()
            .Any(existing =>
                existing.PolygonId == constraint.PolygonId &&
                existing.EdgeId == constraint.EdgeId
            );
    }

    private bool IsValidPerpendicularConstraint(PerpendicularEdgesConstraint constraint)
    {
        if (constraint.PolygonId1 == constraint.PolygonId2 &&
            constraint.EdgeId1 == constraint.EdgeId2)
        {
            return false;
        }

        var polygon1 = _polygons.FirstOrDefault(p => p.Id == constraint.PolygonId1);
        var polygon2 = _polygons.FirstOrDefault(p => p.Id == constraint.PolygonId2);
        
        var edge1 = polygon1?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId1);
        var edge2 = polygon2?.Edges.FirstOrDefault(e => e.Id == constraint.EdgeId2);
        
        if (edge1 == null ||  edge2 == null)
            return false;
        
        if (edge1.Geometry is not LineGeometry || edge2.Geometry is not LineGeometry)
        {
            return false;
        }

        return !_constraints
            .OfType<PerpendicularEdgesConstraint>()
            .Any(existing => SameEdgePair(existing, constraint));
    }

    private static bool SameEdgePair(PerpendicularEdgesConstraint first, PerpendicularEdgesConstraint second)
    {
        bool sameOrder =
            first.PolygonId1 == second.PolygonId1 &&
            first.EdgeId1 == second.EdgeId1 &&
            first.PolygonId2 == second.PolygonId2 &&
            first.EdgeId2 == second.EdgeId2;

        bool reversedOrder =
            first.PolygonId1 == second.PolygonId2 &&
            first.EdgeId1 == second.EdgeId2 &&
            first.PolygonId2 == second.PolygonId1 &&
            first.EdgeId2 == second.EdgeId1;

        return sameOrder || reversedOrder;
    }
}