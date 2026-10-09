using PolygonStudio.Core.Constraints;

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
        _polygons.Add(new Polygon());
        // _polygons.Add(new Polygon());
    }
    
}