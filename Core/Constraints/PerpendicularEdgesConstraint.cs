namespace PolygonStudio.Core.Constraints;

public sealed class PerpendicularEdgesConstraint(
    Guid polygonId1, Guid edgeId1,
    Guid polygonId2, Guid edgeId2
) : Constraint
{
    public Guid PolygonId1 { get; } = polygonId1;
    public Guid EdgeId1 { get; } = edgeId1;
    
    public Guid PolygonId2 { get; } = polygonId2;
    public Guid EdgeId2 { get; } = edgeId2;
}