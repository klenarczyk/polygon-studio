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

        if (constraint is FixedEdgeLengthConstraint fixedLength)
        {
            var polygon = _polygons.FirstOrDefault(p => p.Id == fixedLength.PolygonId);
            var edge = polygon?.Edges.FirstOrDefault(e => e.Id == fixedLength.EdgeId);

            if (edge is null)
                return false;

            if (_constraints
                .OfType<FixedEdgeLengthConstraint>()
                .Any(c => 
                    c.PolygonId == fixedLength.PolygonId && 
                    c.EdgeId == fixedLength.EdgeId)
                )
            {
                return false;
            }
        }
        
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
        _constraints.RemoveAll(c =>
            c is FixedEdgeLengthConstraint fixedLength &&
            fixedLength.PolygonId == polygonId &&
            fixedLength.EdgeId == edgeId
        );
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
        var polygon1 = new Polygon();
        var polygon2 = new Polygon();

        polygon1.Vertices[0].Position = new Point2D(-100.0, 0.0);
        polygon1.Vertices[1].Position = new Point2D(-55.0, 50.0);
        polygon1.Vertices[2].Position = new Point2D(-10.0, 0.0);
        
        polygon2.Vertices[0].Position = new Point2D(10.0, 50.0);
        polygon2.Vertices[1].Position = new Point2D(55.0, 0.0);
        polygon2.Vertices[2].Position = new Point2D(100.0, 50.0);
        
        polygon1.Edges[0].Geometry = new CubicBezierGeometry(
            new Point2D(-100.0, 25.0),
            new Point2D(-55.0, 25.0)
        );

        polygon2.Edges[0].Geometry = CircularArcGeometry.CreateSemicircle();

        var constraint = new FixedEdgeLengthConstraint(polygon1.Id, polygon1.Edges[2].Id, 90.0);
        
        _polygons.Add(polygon1);
        _polygons.Add(polygon2);
        
        _constraints.Add(constraint);
    }
    
}