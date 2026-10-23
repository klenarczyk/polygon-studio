namespace PolygonStudio.Core.Constraints;

public sealed class PerpendicularEdgesConstraint : Constraint
{
    public Guid PolygonId1 { get; }
    public Guid EdgeId1 { get; }
    
    public Guid PolygonId2 { get; }
    public Guid EdgeId2 { get; }
    
    public PerpendicularEdgesConstraint(
        Guid polygonId1, Guid edgeId1,
        Guid polygonId2, Guid edgeId2)
    {
        if (edgeId1 == edgeId2)
            throw new ArgumentException($"Perpendicular constraints require two distinct edges.");
        
        PolygonId1 = polygonId1;
        EdgeId1 = edgeId1;
        
        PolygonId2 = polygonId2;
        EdgeId2 = edgeId2;
    }
}