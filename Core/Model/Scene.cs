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
        // TODO: Check if constraint is satisfiable
        
        _constraints.Add(constraint);
        return true;
    }

    public void RemoveConstraint(Guid constraintId)
    {
        foreach (var constraint in _constraints)
        {
            if (constraint.Id != constraintId) continue;
            
            _constraints.Remove(constraint);
            return;
        }
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
        polygon1.Edges[0].Geometry = new CubicBezierGeometry(
            new Point2D(0.0, 0.0),
            new Point2D(0.5, 0.5)
        );
        
        _polygons.Add(polygon1);
        // _polygons.Add(new Polygon());
    }
    
}