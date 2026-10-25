using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services.Transactions;

internal sealed class SceneSnapshot
{
    private readonly Polygon[] _polygons;
    private readonly IConstraint[] _constraints;
    private readonly PolygonSnapshot[] _polygonSnapshots;
    private readonly (FixedLengthConstraint Constraint, double Length)[] _fixedLengthSnapshots;

    private SceneSnapshot(Scene scene)
    {
        _polygons = [.. scene.Polygons];
        _constraints = [.. scene.Constraints];

        _polygonSnapshots =
        [
            .. _polygons.Select(polygon => new PolygonSnapshot(polygon))
        ];

        _fixedLengthSnapshots =
        [
            .. _constraints
                .OfType<FixedLengthConstraint>()
                .Select(constraint => (constraint, constraint.Length))
        ];
    }

    public static SceneSnapshot Capture(Scene scene) => new(scene);

    public void Restore(Scene scene)
    {
        foreach (var polygonSnapshot in _polygonSnapshots)
            polygonSnapshot.Restore();

        foreach ((var constraint, double length) in _fixedLengthSnapshots)
            constraint.Length = length;

        scene.RestoreContents(_polygons, _constraints);
    }

    private sealed class PolygonSnapshot
    {
        private readonly Polygon _polygon;
        private readonly Vertex[] _vertices;
        private readonly Edge[] _edges;
        private readonly VertexState[] _vertexStates;
        private readonly EdgeState[] _edgeStates;

        public PolygonSnapshot(Polygon polygon)
        {
            _polygon = polygon;
            _vertices = [.. polygon.Vertices];
            _edges = [.. polygon.Edges];

            _vertexStates =
            [
                .. _vertices.Select(vertex => new VertexState(
                        vertex,
                        vertex.Position,
                        vertex.Continuity))
            ];

            _edgeStates =
            [
                .. _edges.Select(edge => new EdgeState(edge, edge.Geometry))
            ];
        }

        public void Restore()
        {
            _polygon.RestoreContents(_vertices, _edges);

            foreach (var state in _vertexStates)
            {
                state.Vertex.Position = state.Position;
                state.Vertex.Continuity = state.Continuity;
            }

            foreach (var state in _edgeStates)
                state.Edge.Geometry = state.Geometry;
        }
    }

    private sealed record VertexState(
        Vertex Vertex,
        Geometry.Point2D Position,
        ContinuityType Continuity
    );

    private sealed record EdgeState(
        Edge Edge,
        EdgeGeometry Geometry
    );
}
