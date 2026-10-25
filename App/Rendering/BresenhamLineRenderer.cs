using System;
using Avalonia;
using Avalonia.Media;

namespace PolygonStudio.App.Rendering;

public static class BresenhamLineRenderer
{
    public static void Draw(DrawingContext context, Pen pen, Point start, Point end)
    {
        var x0 = (int)Math.Round(start.X);
        var y0 = (int)Math.Round(start.Y);
        var x1 = (int)Math.Round(end.X);
        var y1 = (int)Math.Round(end.Y);

        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int error = dx + dy;

        // Pixel == 1x1 square
        double pixelSize = Math.Max(1.0, pen.Thickness);

        while (true)
        {
            context.FillRectangle(
                pen.Brush ?? Brushes.Black,
                new Rect(x0, y0, pixelSize, pixelSize)
            );

            if (x0 == x1 && y0 == y1)
                break;

            int err2 = 2 * error;

            if (err2 > dy)
            {
                error += dy;
                x0 += sx;
            }

            if (err2 < dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }
}