using PolygonStudio.Core.Geometry;

namespace PolygonStudio.Core.Model;

public sealed class Polygon
{
    public Guid Id { get; } = Guid.NewGuid();

    private readonly List<Vertex> _vertices = [];
    public IReadOnlyList<Vertex> Vertices => _vertices;
    
    private readonly List<Edge> _edges = [];
    public IReadOnlyList<Edge> Edges => _edges;

    public Polygon(IReadOnlyList<Point2D> vertices)
    {
        if (vertices.Count < 3)
            throw new ArgumentException("A polygon must have at least 3 vertices.", nameof(vertices));

        foreach (var vertex in vertices)
            AddVertex(new Vertex(vertex));

        for (var i = 0; i < vertices.Count; i++)
        {
            var fromId = _vertices[i].Id;
            var toId = _vertices[(i + 1) % vertices.Count].Id;
            
            AddEdge(new Edge(fromId, toId));
        }
    }
    
    internal void AddVertex(Vertex vertex)
    {
        if (_vertices.Any(v => v.Id == vertex.Id))
            return;
        
        _vertices.Add(vertex);
    }
    
    internal bool RemoveVertex(Guid id)
    {
        if (_vertices.Count <= 3)
            return false;
        
        var vertex = _vertices.FirstOrDefault(v => v.Id == id);
        return vertex is not null && _vertices.Remove(vertex);
    }
    
    internal void AddEdge(Edge edge)
    {
        if (_edges.Any(e => e.Id == edge.Id))
            return;
        
        _edges.Add(edge);
    }

    internal bool RemoveEdge(Guid id)
    {
        var edge = _edges.FirstOrDefault(e => e.Id == id);
        return edge is not null && _edges.Remove(edge);
    }
    
    
    internal void RestoreContents(IReadOnlyList<Vertex> vertices, IReadOnlyList<Edge> edges)
    {
        _vertices.Clear();
        _vertices.AddRange(vertices);

        _edges.Clear();
        _edges.AddRange(edges);
    }

}