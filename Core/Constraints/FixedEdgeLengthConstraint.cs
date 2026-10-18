namespace PolygonStudio.Core.Constraints;

public sealed class FixedEdgeLengthConstraint : Constraint
{
    public Guid PolygonId { get; }
    public Guid EdgeId { get; }
    public double Length { get; }
    
    public FixedEdgeLengthConstraint(Guid polygonId, Guid edgeId, double length)
    {
        if (length < 0 || !double.IsFinite(length)) 
            throw new ArgumentOutOfRangeException(nameof(length));
        
        PolygonId = polygonId;
        EdgeId = edgeId;
        Length = length;
    }
}