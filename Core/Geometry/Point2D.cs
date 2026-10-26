namespace PolygonStudio.Core.Geometry;

public readonly record struct Point2D(double X = 0.0, double Y = 0.0)
{
    public static Point2D operator -(Point2D operand)
        => new(-operand.X, -operand.Y);
    
    public static Point2D operator +(Point2D left, Point2D right)
        => new(left.X + right.X, left.Y + right.Y);

    public static Point2D operator -(Point2D left, Point2D right)
        => new(left.X - right.X, left.Y - right.Y);
    
    public static Point2D operator *(Point2D point, double factor)
        => new(point.X * factor, point.Y * factor);
    
    public static Point2D operator *(double factor, Point2D point)
        => new(point.X * factor, point.Y * factor);

    public static Point2D operator /(Point2D point, double factor)
    {
        return factor == 0.0 
            ? throw new DivideByZeroException() 
            : new Point2D(point.X / factor, point.Y / factor);
    }
}