using System;
using Avalonia.Controls;
using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Controls.Canvas;

public static class VertexContextMenu
{
    public static void Show(
        Control owner,
        EditorService editor,
        Guid polygonId, Guid vertexId,
        Action invalidateVisual)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Pointer
        };

        // ---
        
        var continuityMenu = new MenuItem()
        {
            Header = "Continuity"
        };

        var currentType = editor.GetVertexContinuity(polygonId, vertexId);
        
        foreach (var type in Enum.GetValues<ContinuityType>())
        {
            var item = new MenuItem
            {
                Header = type.ToString(),
                IsEnabled = true,
                IsChecked = currentType == type
            };

            item.Click += (_, _) =>
            {
                if (editor.SetVertexContinuity(polygonId, vertexId, type))
                    invalidateVisual();
            };

            continuityMenu.Items.Add(item);
        }
        
        menu.Items.Add(continuityMenu);
        
        // ---
        
        var deleteItem = new MenuItem
        {
            Header = "Delete vertex"
        };

        deleteItem.Click += (_, _) =>
        {
            if (editor.RemoveVertex(polygonId, vertexId))
                invalidateVisual();
        };

        menu.Items.Add(deleteItem);
        menu.Open(owner);
    }
}