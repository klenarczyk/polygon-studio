namespace PolygonStudio.Core.Services.Constraints;

public enum MoveTargetKind
{
    Vertex,
    BezierControlPoint
}

public readonly record struct MoveTarget
{
    public MoveTargetKind Kind { get; }
    public Guid PolygonId { get; }
    public Guid ElementId { get; }
    public int ControlPointIndex { get; }

    private MoveTarget(MoveTargetKind kind, Guid polygonId, Guid elementId, int controlPointIndex = -1)
    {
        if (kind == MoveTargetKind.BezierControlPoint && controlPointIndex is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(controlPointIndex));

        Kind = kind;
        PolygonId = polygonId;
        ElementId = elementId;
        ControlPointIndex = controlPointIndex;
    }
    
    public static MoveTarget Vertex(Guid polygonId, Guid vertexId) =>
        new(MoveTargetKind.Vertex, polygonId, vertexId);

    public static MoveTarget BezierControlPoint(
        Guid polygonId,
        Guid edgeId,
        int controlPointIndex)
    {
        if (controlPointIndex is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(controlPointIndex));

        return new MoveTarget(
            MoveTargetKind.BezierControlPoint,
            polygonId,
            edgeId,
            controlPointIndex
        );
    }
}