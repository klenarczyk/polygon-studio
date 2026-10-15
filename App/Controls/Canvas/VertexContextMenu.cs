using System;
using Avalonia.Controls;
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

        continuityMenu.Items.Add(new Separator());
        
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