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

        AddItem("Line", typeof(LineGeometry));
        AddItem("Cubic Bezier", typeof(CubicBezierGeometry));
        AddItem("Circular arc", typeof(CircularArcGeometry));
        
        menu.Open(owner);
        return;

        void AddItem(string label, Type geometryType)
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
                {
                    invalidateVisual();
                }
            };

            menu.Items.Add(item);
        }
    }
}