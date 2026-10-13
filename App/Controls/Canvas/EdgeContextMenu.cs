using System;
using Avalonia.Controls;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Controls.Canvas;

public static class EdgeContextMenu
{
    public static void Show(
        Control owner,
        EditorService editor,
        Guid polygonId, Guid edgeId,
        EdgeGeometry currentGeometry,
        Action invalidateVisual)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Pointer
        };

        // ---
        
        var geometryMenu = new MenuItem()
        {
            Header = "Geometry"
        };
        
        AddGeometryItem("Line", typeof(LineGeometry));
        AddGeometryItem("Cubic Bezier", typeof(CubicBezierGeometry));
        AddGeometryItem("Circular arc", typeof(CircularArcGeometry));
        
        menu.Items.Add(geometryMenu);
        
        // ---
        
        var constraintMenu = new MenuItem()
        {
            Header = "Constraints"
        };
        
        constraintMenu.Items.Add(new Separator());
        
        menu.Items.Add(constraintMenu);
        menu.Items.Add(new Separator());
        
        // ---
        
        var splitItem = new MenuItem
        {
            Header = "Split edge"
        };

        splitItem.Click += (_, _) =>
        {
            if (editor.SplitEdge(polygonId, edgeId))
                invalidateVisual();
        };

        menu.Items.Add(splitItem);
        menu.Open(owner);
        return;

        // ---
        
        void AddGeometryItem(string label, Type geometryType)
        {
            bool isCurrent = currentGeometry.GetType() == geometryType;

            var item = new MenuItem
            {
                Header = label,
                IsEnabled = !isCurrent
            };

            item.Click += (_, _) =>
            {
                if (editor.SetEdgeGeometry(polygonId, edgeId, geometryType))
                    invalidateVisual();
            };

            geometryMenu.Items.Add(item);
        }
    }
}