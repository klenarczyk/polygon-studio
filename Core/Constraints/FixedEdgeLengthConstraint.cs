namespace PolygonStudio.Core.Constraints;

public sealed class FixedEdgeLengthConstraint : Constraint
{
    public Guid PolygonId { get; }
    public Guid EdgeId { get; }
    public double Length { get; }
    
    public FixedEdgeLengthConstraint(Guid polygonId, Guid edgeId, double length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        PolygonId = polygonId;
        EdgeId = edgeId;
        Length = length;
    }
}