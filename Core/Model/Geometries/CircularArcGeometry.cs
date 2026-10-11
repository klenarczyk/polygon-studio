using PolygonStudio.Core.Geometry;

namespace PolygonStudio.Core.Model.Geometries;

public record CircularArcGeometry(double Bulge) : EdgeGeometry
{
    public static CircularArcGeometry CreateSemicircle(bool counterClockwise = true)
    {
        return new CircularArcGeometry(counterClockwise ? 1.0 : -1.0);
    }
}