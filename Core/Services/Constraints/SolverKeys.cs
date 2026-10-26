using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Model;

namespace PolygonStudio.Core.Services.Constraints;

internal readonly record struct VertexKey(Guid PolygonId, Guid VertexId);
internal readonly record struct EdgeKey(Guid PolygonId, Guid EdgeId);

internal sealed record ResolvedFixedConstraint(
    VertexKey From, VertexKey To,
    double EdgeLength
);

internal sealed record ResolvedPerpendicularConstraint(
    VertexKey From1, VertexKey To1,
    VertexKey From2, VertexKey To2
);

internal sealed record ResolvedContinuity(
    Polygon Polygon, Guid VertexId,
    ContinuityType Type,
    Edge Incoming, Edge Outgoing
);