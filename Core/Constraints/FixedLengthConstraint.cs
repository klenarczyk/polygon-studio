namespace PolygonStudio.Core.Constraints;

public sealed class FixedLengthConstraint : IConstraint
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public Guid PolygonId { get; }
    public Guid EdgeId { get; }
    public double Length { get; internal set; }
    
    public FixedLengthConstraint(Guid polygonId, Guid edgeId, double length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        PolygonId = polygonId;
        EdgeId = edgeId;
        Length = length;
    }
}