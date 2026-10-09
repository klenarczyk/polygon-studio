namespace PolygonStudio.Core.Model;

public sealed class Edge(Guid fromVertexId, Guid toVertexId)
{
    public Guid Id { get; } = Guid.NewGuid();

    public Guid FromVertexId { get; } = fromVertexId;
    public Guid ToVertexId { get; } = toVertexId;

    public IEdgeGeometry Geometry { get; } = null!; // TODO
}