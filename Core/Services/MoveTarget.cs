namespace PolygonStudio.Core.Services;

public enum MoveTargetKind
{
    Vertex,
    BezierControlPoint
}

public readonly record struct MoveTarget(
    MoveTargetKind Kind,
    Guid PolygonId,
    Guid ElementId,
    int ControlPointIndex = -1)
{
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