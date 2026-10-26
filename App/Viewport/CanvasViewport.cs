using Avalonia;

namespace PolygonStudio.App.Viewport;

public sealed class CanvasViewport
{
    public ViewTransform? Transform { get; private set; }

    public void Initialize(Size viewportSize)
    {
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
            return;

        Transform = new ViewTransform(
            1, 
            viewportSize.Width / 2, viewportSize.Height / 2, 
            0, 0
        );
    }

    public void PanBy(Point canvasDelta)
    {
        Transform = Transform?.PanBy(canvasDelta);
    }

    public void ZoomAt(Point canvasPosition, double factor)
    {
        Transform = Transform?.ZoomAt(canvasPosition, factor);
    }
}