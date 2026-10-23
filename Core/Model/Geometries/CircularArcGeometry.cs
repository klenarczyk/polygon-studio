namespace PolygonStudio.Core.Model.Geometries;

public record CircularArcGeometry(double Bulge) : EdgeGeometry
{
    public static CircularArcGeometry CreateSemicircle()
    {
        return new CircularArcGeometry(1.0);
    }
}