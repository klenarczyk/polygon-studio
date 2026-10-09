using PolygonStudio.Core.Geometry;

namespace PolygonStudio.Core.Model;

public sealed class Vertex
{
    public Guid Id { get; } = Guid.NewGuid();
    public Point2D Position { get; }

    public Vertex(Point2D position)
    {
        Position = position;
    }

    public Vertex(double x, double y)
    {
        Position = new Point2D(x, y);
    }
    
}