namespace PolygonStudio.Core.Constraints;

public abstract class Constraint
{
    public Guid Id { get; } = Guid.NewGuid();
}