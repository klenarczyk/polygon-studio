using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PolygonStudio.App.Rendering;

public static class BresenhamLineRenderer
{
    public static void Draw(DrawingContext context, Pen pen, Point start, Point end)
    {
        var x0 = (int)Math.Round(start.X);
        var y0 = (int)Math.Round(start.Y);
        var x1 = (int)Math.Round(end.X);
        var y1 = (int)Math.Round(end.Y);

        bool[,] pixels = Rasterize(x0, y0, x1, y1, out int originX, out int originY);
        
        int width = pixels.GetLength(0);
        int height = pixels.GetLength(1);

        var color = pen.Brush is ISolidColorBrush brush
            ? brush.Color
            : Colors.Black;

        using var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul
        );

        using (var framebuffer = bitmap.Lock())
        {
            var row = new byte[framebuffer.RowBytes];

            byte alpha = color.A;
            var red = (byte)(color.R * alpha / 255);
            var green = (byte)(color.G * alpha / 255);
            var blue = (byte)(color.B * alpha / 255);

            for (var y = 0; y < height; y++)
            {
                Array.Clear(row);

                for (var x = 0; x < width; x++)
                {
                    if (!pixels[x, y]) continue;

                    int index = x * 4;

                    row[index] = blue;
                    row[index + 1] = green;
                    row[index + 2] = red;
                    row[index + 3] = alpha;
                }
                
                Marshal.Copy(
                    row, 0,
                    IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                    row.Length
                );
            }
        }

        var dest = new Rect(originX, originY, width, height);
        context.DrawImage(bitmap, dest);
    }

    private static bool[,] Rasterize(
        int x0, int y0,
        int x1, int y1,
        out int originX, out int originY)
    {
        originX = Math.Min(x0, x1);
        originY = Math.Min(y0, y1);

        int width = Math.Abs(x1 - x0) + 1;
        int height = Math.Abs(y1 - y0) + 1;

        var pixels = new bool[width, height];

        x0 -= originX;
        y0 -= originY;
        x1 -= originX;
        y1 -= originY;
        
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int error = dx + dy;
        
        while (true)
        {
            pixels[x0, y0] = true;

            if (x0 == x1 && y0 == y1)
                break;

            int err2 = 2 * error;

            if (err2 >= dy)
            {
                 error += dy;
                x0 += sx;
            }

            if (err2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }

        return pixels;
    }
}