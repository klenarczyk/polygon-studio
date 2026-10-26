using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using PolygonStudio.App.ContextMenus;
using PolygonStudio.App.Interaction;
using PolygonStudio.App.Rendering;
using PolygonStudio.App.Viewport;
using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services;
using LineGeometry = PolygonStudio.Core.Model.Geometries.LineGeometry;

namespace PolygonStudio.App.Controls;

public sealed class PolygonCanvas : Control
{
    public Scene? Scene { get; set; }
    public EditorService? Editor { get; set; }

    public LineDrawingAlgorithm LineDrawingAlgorithm
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateVisual();
        }
    } = LineDrawingAlgorithm.Avalonia;

    private readonly CanvasViewport _viewport = new();
    private readonly CanvasInteractionController _interaction = new();
    
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        
        if (_viewport.Transform is null) 
            _viewport.Initialize(Bounds.Size);

        if (_viewport.Transform is not { } transform)
            return;
        
        PolygonCanvasRenderer.Draw(
            context,
            Scene,
            transform,
            _interaction.Selection,
            _interaction.Drag,
            LineDrawingAlgorithm
        );
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;

        if (_interaction.TryBeginPan(point, e.KeyModifiers))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        
        if (properties.IsRightButtonPressed)
        {
            HandleRightClick(e);
            return;
        }
        
        if (!properties.IsLeftButtonPressed)
            return;
        
        if (_viewport.Transform is null) 
            _viewport.Initialize(Bounds.Size);

        if (_viewport.Transform is not { } transform)
            return;

        var hitTester = new CanvasHitTester(Scene, transform);
        
        bool handled = _interaction.HandlePointerPressed(
            point, 
            hitTester, 
            e.KeyModifiers
        );

        if (_interaction.Drag.IsDragging)
            e.Pointer.Capture(this);
        
        InvalidateVisual();
        e.Handled = handled || _interaction.Drag.IsDragging;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_interaction.HandlePanPointerMoved(e.GetPosition(this), _viewport, InvalidateVisual))
        {
            e.Handled = true;
            return;
        }
        
        if (_viewport.Transform is not { } transform || Editor is null)
            return;
        
        _interaction.HandlePointerMoved(
            e.GetPosition(this),
            transform,
            Editor,
            InvalidateVisual
        );
        
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        if (_interaction.EndPan())
        {
            if (Equals(e.Pointer.Captured, this))
                e.Pointer.Capture(null);

            InvalidateVisual();
            e.Handled = true;
            return;
        }
        
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        _interaction.HandlePointerReleased(InvalidateVisual);
        
        if (Equals(e.Pointer.Captured, this))
            e.Pointer.Capture(null);
        
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        
        _interaction.EndPan();
        _interaction.Drag.Reset();
        
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (_viewport.Transform is null)
            _viewport.Initialize(Bounds.Size);

        if (_viewport.Transform is null)
            return;

        _interaction.HandlePointerWheelChanged(
            e.GetPosition(this),
            e.Delta.Y,
            _viewport
        );

        InvalidateVisual();
        e.Handled = true;
    }
    
    // ---

    private void HandleRightClick(PointerPressedEventArgs e)
    {
        if (Scene is null || Editor is null || _viewport.Transform is not { } transform)
            return;

        var hitTester = new CanvasHitTester(Scene, transform);
        var pointerPos = e.GetPosition(this);
        
        var vertexHit = hitTester.FindVertexAt(pointerPos);
        if (vertexHit is { } vertexRes)
        {
            _interaction.SelectVertexForContextMenu(vertexRes.Polygon.Id, vertexRes.Vertex.Id);

            InvalidateVisual();

            VertexContextMenu.Show(
                this,
                Editor,
                vertexRes.Polygon.Id, vertexRes.Vertex.Id,
                InvalidateVisual
            );

            e.Handled = true;
            return;
        }

        var edgeHit = hitTester.FindEdgeAt(pointerPos);
        if (edgeHit is { } edgeRes)
        {
            _interaction.SelectEdgeForContextMenu(edgeRes.Polygon.Id, edgeRes.Edge.Id);

            InvalidateVisual();

            var selectedEdges = _interaction.Selection.Edges;
            bool canAddPerpendicular = selectedEdges.Count == 2;

            EdgeContextMenu.Show(
                this,
                Editor,
                edgeRes.Polygon.Id,
                edgeRes.Edge.Id,
                edgeRes.Edge.Geometry,
                InvalidateVisual,
                canAddPerpendicular,
                TryAddPerpendicularConstraint
            );

            e.Handled = true;
            return;
        }

        _interaction.Reset();
        InvalidateVisual();
        e.Handled = true;
    }
    
    // ---
    
    public bool TryAddPerpendicularConstraint()
    {
        var edgeSet = _interaction.Selection.Edges;
        
        if (Editor is null || edgeSet.Count != 2)
            return false;

        var selected = edgeSet.ToArray();
        
        var first = selected[0];
        var second = selected[1];

        bool added = Editor.AddPerpendicularConstraint(
            first.PolygonId, first.EdgeId,
            second.PolygonId, second.EdgeId
        );

        if (added)
        {
            _interaction.Reset();
            InvalidateVisual();
        }

        return added;
    }
}