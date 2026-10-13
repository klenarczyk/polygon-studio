namespace PolygonStudio.Core.Model;

public sealed class Polygon
{
    public Guid Id { get; } = Guid.NewGuid();

    private readonly List<Vertex> _vertices = [];
    public IReadOnlyList<Vertex> Vertices => _vertices;
    
    private readonly List<Edge> _edges = [];
    public IReadOnlyList<Edge> Edges => _edges;

    public Polygon()
    {
        _vertices.Add(new Vertex(-1.0, 0.0));
        _vertices.Add(new Vertex(0.0, 1.0));
        _vertices.Add(new Vertex(1.0, 0.0));
        
        _edges.Add(new Edge(_vertices[0].Id, _vertices[1].Id));
        _edges.Add(new Edge(_vertices[1].Id, _vertices[2].Id));
        _edges.Add(new Edge(_vertices[2].Id, _vertices[0].Id));
    }
    
    internal void AddVertex(Vertex vertex)
    {
        if (_vertices.FirstOrDefault(v => v.Id == vertex.Id) != null)
            return;
        
        _vertices.Add(vertex);
    }
    
    internal bool RemoveVertex(Guid id)
    {
        throw new NotImplementedException();
    }
    
    internal void AddEdge(Edge edge)
    {
        if (_edges.FirstOrDefault(e => e.Id == edge.Id) != null)
            return;
        
        _edges.Add(edge);
    }

    internal bool RemoveEdge(Guid id)
    {
        int numRemoved = _edges.RemoveAll(e => e.Id == id);
        return numRemoved > 0;
    }
}