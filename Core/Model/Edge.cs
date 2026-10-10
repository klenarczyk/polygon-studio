using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Model;

public sealed class Edge(Guid fromVertexId, Guid toVertexId, EdgeGeometry? geometry = null)
{
    public Guid Id { get; } = Guid.NewGuid();

    public Guid FromVertexId { get; } = fromVertexId;
    public Guid ToVertexId { get; } = toVertexId;

    // public EdgeGeometry Geometry { get; internal set; } = geometry ?? new LineGeometry();
    public EdgeGeometry Geometry { get; internal set; } 
        = geometry 
          ?? (Random.Shared.Next() % 3 == 0 
              ? new CubicBezierGeometry(new Point2D(0.0, 0.0), new Point2D(0.5, 0.5)) 
              : new LineGeometry()); // TODO: Remove
}