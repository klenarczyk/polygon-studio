using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services;
using LineGeometry = PolygonStudio.Core.Model.Geometries.LineGeometry;

namespace PolygonStudio.App.Controls;

public sealed class PolygonCanvas : Control
{
    private const double VertexRadius = 5.0;
    private const double ControlPointSize = 8.0;
    
    private const double HitRadius = 10.0;
    private const double EdgeHitTolerance = 8.0;
    private const int CurveHitTestSteps = 64;
    
    private const double Padding = 30.0;

    private Guid? _selectedPolygonId;
    private Guid? _selectedEdgeId;
    
    private DragTarget _dragTarget = DragTarget.None;
    private Guid? _draggedPolygonId;
    private Guid? _draggedVertexId;
    private Guid? _draggedEdgeId;
    private int _draggedControlPointIdx = -1;
    
    private ViewTransform? _viewTransform;
    
    public Scene? Scene { get; set; }
    public EditorService? Editor { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        
        EnsureViewTransform();
        if (_viewTransform is not { } transform) return;
        
        var edgePen = new Pen(Brushes.Black, 2);
        var selectedEdgePen = new Pen(Brushes.Coral, 2);
        var vertexPen = new Pen(Brushes.Black, 2);
        var controlPen = new Pen(Brushes.SlateGray, 1, DashStyle.Dash);
        
        // Edges
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
                var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);
                
                if (from is null || to is null) continue;

                var start = transform.ToScreen(from.Position);
                var end = transform.ToScreen(to.Position);

                bool isSelected = polygon.Id == _selectedPolygonId &&
                                  edge.Id == _selectedEdgeId;

                var currentEdgePen = isSelected ? selectedEdgePen : edgePen;
                
                switch (edge.Geometry) // TODO: Add an edge drawing interface later on
                {
                    case LineGeometry:
                        context.DrawLine(currentEdgePen, start, end);
                        break;
                    
                    case CubicBezierGeometry bezier:
                        DrawBezier(
                            context,
                            currentEdgePen, controlPen,
                            transform,
                            from.Position, to.Position,
                            bezier);
                        break;
                    
                    case CircularArcGeometry arc:
                        DrawCircularArc(
                            context,
                            currentEdgePen,
                            transform,
                            from.Position, to.Position,
                            arc);
                        break;
                }
            }
        }
        
        // Control points
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                if (edge.Geometry is not CubicBezierGeometry bezier)
                    continue;

                bool isControlSelected = _dragTarget == DragTarget.BezierControlPoint &&
                                         _draggedEdgeId == edge.Id;
                
                DrawControlPoint(
                    context,
                    transform.ToScreen(bezier.ControlPoint1),
                    isControlSelected && _draggedControlPointIdx == 0);
                
                DrawControlPoint(
                    context,
                    transform.ToScreen(bezier.ControlPoint2),
                    isControlSelected && _draggedControlPointIdx == 1);
            }
        }
        
        // Vertices
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var point = transform.ToScreen(vertex.Position);
                bool isSelected = _draggedVertexId == vertex.Id;
                
                context.DrawEllipse(
                    isSelected ? Brushes.LightBlue : Brushes.White,
                    vertexPen,
                    point,
                    VertexRadius,
                    VertexRadius);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (_selectedEdgeId is not null)
        {
            _selectedPolygonId = null;
            _selectedEdgeId = null;
            InvalidateVisual();
        }
        
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            HandleRightClick(e);
            return;
        }
        
        base.OnPointerPressed(e);

        if (Scene is null || Editor is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        
        var pointerPos = e.GetPosition(this);
        
        EnsureViewTransform();
        if (_viewTransform is not { } transform) return;

        double bestDistSquared = HitRadius * HitRadius;
        Guid? foundPolygonId = null;
        Guid? foundVertexId = null;
        
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var screenPos = transform.ToScreen(vertex.Position);
                double dx = screenPos.X - pointerPos.X;
                double dy = screenPos.Y - pointerPos.Y;
                double distSquared = dx * dx + dy * dy;

                if (!(distSquared < bestDistSquared)) continue;
                
                bestDistSquared = distSquared;
                foundPolygonId = polygon.Id;
                foundVertexId = vertex.Id;
            }
        }

        if (foundPolygonId is not null && foundVertexId is not null)
        {
            _dragTarget = DragTarget.Vertex;
            _draggedPolygonId = foundPolygonId;
            _draggedVertexId = foundVertexId;
            _draggedEdgeId = null;
            _draggedControlPointIdx = -1;
        
            e.Pointer.Capture(this);
            e.Handled = true;
        
            InvalidateVisual();
        }
        
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                if (edge.Geometry is not CubicBezierGeometry bezier)
                    continue;

                Point[] controlPoints =
                [
                    transform.ToScreen(bezier.ControlPoint1),
                    transform.ToScreen(bezier.ControlPoint2)
                ];

                for (var i = 0; i < controlPoints.Length; i++)
                {
                    var point = controlPoints[i];

                    double dx = point.X - pointerPos.X;
                    double dy = point.Y - pointerPos.Y;

                    if (dx * dx + dy * dy > HitRadius * HitRadius)
                        continue;

                    _dragTarget = DragTarget.BezierControlPoint;
                    _draggedPolygonId = polygon.Id;
                    _draggedEdgeId = edge.Id;
                    _draggedControlPointIdx = i;
                    _draggedVertexId = null;

                    e.Pointer.Capture(this);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragTarget == DragTarget.None ||
            _draggedPolygonId is not { } polygonId ||
            _viewTransform is not { } transform ||
            Editor is null)
        {
            return;
        }

        var screenPos = e.GetPosition(this);
        var worldPos = transform.ToWorld(screenPos);
        
        bool changed = _dragTarget switch
        {
            DragTarget.Vertex when _draggedVertexId is { } vertexId =>
                Editor.MoveVertex(polygonId, vertexId, worldPos),

            DragTarget.BezierControlPoint when _draggedEdgeId is { } edgeId =>
                Editor.MoveBezierControlPoint(
                    polygonId,
                    edgeId,
                    _draggedControlPointIdx,
                    worldPos),

            _ => false
        };

        if (changed)
            InvalidateVisual();

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragTarget == DragTarget.None) return;

        e.Pointer.Capture(null);

        _dragTarget = DragTarget.None;
        _draggedPolygonId = null;
        _draggedVertexId = null;
        _draggedEdgeId = null;
        _draggedControlPointIdx = -1;
        
        InvalidateVisual();
        e.Handled = true;
    }

    private void ShowEdgeMenu(
        Guid polygonId,
        Guid edgeId,
        EdgeGeometry currentGeometry)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Pointer
        };
        
        AddGeometryMenuItem(
            menu, "Line", typeof(LineGeometry),
            currentGeometry, polygonId, edgeId
        );

        AddGeometryMenuItem(
            menu, "Cubic Bezier", typeof(CubicBezierGeometry),
            currentGeometry, polygonId, edgeId
        );

        AddGeometryMenuItem(
            menu, "Circular arc", typeof(CircularArcGeometry),
            currentGeometry, polygonId, edgeId
        );

        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(ContextMenu, menu)) ContextMenu = null;
        };
        
        ContextMenu = menu;
        menu.Open(this);
    }
    
    private void AddGeometryMenuItem(
        ContextMenu menu,
        string label,
        Type geometryType,
        EdgeGeometry currentGeometry,
        Guid polygonId,
        Guid edgeId)
    {
        bool isCurrent = currentGeometry.GetType() == geometryType;

        var item = new MenuItem
        {
            Header = isCurrent ? $"✓  {label}" : $"    {label}"
        };

        item.Click += (_, _) =>
        {
            if (Editor != null && 
                Editor.SetEdgeGeometry(polygonId, edgeId, geometryType))
            {
                InvalidateVisual();
            }
        };

        menu.Items.Add(item);
    }
    
    // ---

    private void HandleRightClick(PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);

        var hit = FindEdgeAt(point);
        if (hit is null) return;

        var (polygon, edge) = hit.Value;
        
        _selectedPolygonId = polygon.Id;
        _selectedEdgeId = edge.Id;
        
        InvalidateVisual();
        
        ShowEdgeMenu(polygon.Id, edge.Id, edge.Geometry);

        e.Handled = true;
    }

    private (Polygon Polygon, Edge Edge)? FindEdgeAt(Point screenPoint)
    {
        if (Scene is null) return null;

        EnsureViewTransform();
        if (_viewTransform is not { } transform) return null;
        
        (Polygon Polygon, Edge Edge)? bestHit = null;
        double bestDistance = EdgeHitTolerance;
        
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                var start = polygon.Vertices.First(v => v.Id == edge.FromVertexId).Position;
                var end = polygon.Vertices.First(v => v.Id == edge.ToVertexId).Position;

                var previous = transform.ToScreen(start);

                for (var i = 1; i <= GetHitTestSteps(edge.Geometry); i++)
                {
                    double t = (double)i / GetHitTestSteps(edge.Geometry);
                    var current = transform.ToScreen(
                        EvaluateEdge(edge.Geometry, start, end, t));

                    var distance = DistanceToSegment(
                        screenPoint, previous, current);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestHit = (polygon, edge);
                    }

                    previous = current;
                }
            }
        }

        return bestHit;
    }

    private static int GetHitTestSteps(EdgeGeometry geometry) => geometry is LineGeometry ? 1 : CurveHitTestSteps;

    private static Point2D EvaluateEdge(
        EdgeGeometry geometry,
        Point2D start, Point2D end,
        double t)
    {
        switch (geometry)
        {
            case CubicBezierGeometry bezier:
            {
                double u = 1.0 - t;
                double a = u * u * u;
                double b = 3.0 * u * u * t;
                double c = 3.0 * u * t * t;
                double d = t * t * t;

                return new Point2D(
                    a * start.X + b * bezier.ControlPoint1.X
                                + c * bezier.ControlPoint2.X + d * end.X,
                    a * start.Y + b * bezier.ControlPoint1.Y
                                + c * bezier.ControlPoint2.Y + d * end.Y);
            }
            
            case CircularArcGeometry arc when 
                CircularArcMath.TryGetParameters(start, end, arc, out var parameters):
            {
                double angle = parameters.StartAngleRadians + parameters.SweepAngleRadians * t;

                return new Point2D(
                    parameters.Center.X + parameters.Radius * Math.Cos(angle),
                    parameters.Center.Y + parameters.Radius * Math.Sin(angle));
            }
            
            default:
                return Lerp(start, end, t);
        }
    }

    private static Point2D Lerp(Point2D a, Point2D b, double t) =>
        new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );

    private static double DistanceToSegment(
        Point point,
        Point a,
        Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 1e-9)
            return Math.Sqrt(
                Math.Pow(point.X - a.X, 2) +
                Math.Pow(point.Y - a.Y, 2));

        double t = Math.Clamp(
            ((point.X - a.X) * dx + (point.Y - a.Y) * dy)
            / lengthSquared,
            0.0, 1.0);

        double closestX = a.X + t * dx;
        double closestY = a.Y + t * dy;

        return Math.Sqrt(
            Math.Pow(point.X - closestX, 2) +
            Math.Pow(point.Y - closestY, 2));
    }
    
    // ---
    
    private void EnsureViewTransform()
    {
        if (_viewTransform is not null)
            return;

        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        _viewTransform = CreateTransform();
    }
    
    private ViewTransform CreateTransform()
    {
        if (Scene is null)
            return new ViewTransform(1, 0, 0, 0, 0);

        var vertices = Scene.Polygons
            .SelectMany(p => p.Vertices)
            .ToList();

        if (vertices.Count == 0)
            return new ViewTransform(1, 0, 0, 0, 0);

        double minX = vertices.Min(v => v.Position.X);
        double maxX = vertices.Max(v => v.Position.X);
        double minY = vertices.Min(v => v.Position.Y);
        double maxY = vertices.Max(v => v.Position.Y);

        double worldWidth = Math.Max(maxX - minX, 1e-6);
        double worldHeight = Math.Max(maxY - minY, 1e-6);

        double availableWidth = Math.Max(Bounds.Width - 2 * Padding, 1);
        double availableHeight = Math.Max(Bounds.Height - 2 * Padding, 1);

        double scale = Math.Min(
            availableWidth / worldWidth,
            availableHeight / worldHeight);

        double drawnWidth = worldWidth * scale;
        double drawnHeight = worldHeight * scale;

        double left = (Bounds.Width - drawnWidth) / 2;
        double top = (Bounds.Height - drawnHeight) / 2;

        return new ViewTransform(scale, left, top, minX, maxY);
    }
    
    // TODO: Move out of PolygonCanvas
    private static void DrawBezier(
        DrawingContext context,
        Pen curvePen, Pen controlPen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CubicBezierGeometry bezier)
    {
        // Control polygon
        var p0 = transform.ToScreen(start);
        var p1 = transform.ToScreen(bezier.ControlPoint1);
        var p2 = transform.ToScreen(bezier.ControlPoint2);
        var p3 = transform.ToScreen(end);
        
        context.DrawLine(controlPen, p0, p1);
        context.DrawLine(controlPen, p1, p2);
        context.DrawLine(controlPen, p2, p3);
        context.DrawLine(controlPen, p3, p0);
        
        // Curve
        const int steps = 64;
        var previous = p0;

        for (var i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double u = 1.0 - t;

            double x =
                u * u * u * start.X +
                3 * u * u * t * bezier.ControlPoint1.X +
                3 * u * t * t * bezier.ControlPoint2.X +
                t * t * t * end.X;

            double y =
                u * u * u * start.Y +
                3 * u * u * t * bezier.ControlPoint1.Y +
                3 * u * t * t * bezier.ControlPoint2.Y +
                t * t * t * end.Y;

            var current = transform.ToScreen(new Point2D(x, y));
            context.DrawLine(curvePen, previous, current);
            previous = current;
        }
    }

    private static void DrawControlPoint(
        DrawingContext context,
        Point screenPoint,
        bool isSelected)
    {
        const double halfSize = ControlPointSize / 2.0;

        var rect = new Rect(
            screenPoint.X - halfSize,
            screenPoint.Y - halfSize,
            ControlPointSize,
            ControlPointSize);
        
        context.DrawRectangle(
            isSelected ? Brushes.LightBlue : Brushes.White,
            new Pen(Brushes.SlateGray, 1.5),
            rect);
    }

    private static void DrawCircularArc(
        DrawingContext context,
        Pen pen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CircularArcGeometry arc)
    {
        if (!CircularArcMath.TryGetParameters(start, end, arc, out var parameters))
            return;

        double startAngle = parameters.StartAngleRadians;
        double radius = parameters.Radius;
        double sweep = parameters.SweepAngleRadians;
        var center = parameters.Center;

        if (radius < 1e-9) return;

        const double maxAngleStep = Math.PI / 64.0;
        
        int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / maxAngleStep));
        var previous = transform.ToScreen(start);

        for (var i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double angle = startAngle + sweep * t;

            var worldPoint = new Point2D(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle));

            var current = i == steps
                ? transform.ToScreen(end)
                : transform.ToScreen(worldPoint);

            context.DrawLine(pen, previous, current);
            previous = current;
        }
    }
    // ---
    
    private readonly record struct ViewTransform(
        double Scale,   // Screen / World
        double Left,    // Horizontal offset
        double Top,     // Vertical offset
        double MinX,    // Min world x
        double MaxY)    // Max world y
    {
        public Point ToScreen(Point2D point) =>
            new(
                Left + (point.X - MinX) * Scale,
                Top + (MaxY - point.Y) * Scale
            );

        public Point2D ToWorld(Point point) =>
            new(
                MinX + (point.X - Left) / Scale,
                MaxY - (point.Y - Top) / Scale
            );
    }

    private enum DragTarget
    {
        None,
        Vertex,
        BezierControlPoint
    }
}