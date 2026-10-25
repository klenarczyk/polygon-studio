using System;
using System.Linq;
using Avalonia.Controls;
using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.ContextMenus;

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

        var continuityMenu = CreateContinuityMenu(editor, polygonId, vertexId, invalidateVisual);
        var deleteItem = CreateDeleteItem(editor, polygonId, vertexId, invalidateVisual);
        
        // ---
        
        menu.Items.Add(continuityMenu);
        menu.Items.Add(deleteItem);
        
        menu.Open(owner);
    }

    // ---
    
    private static MenuItem CreateContinuityMenu(
        EditorService editor,
        Guid polygonId, Guid vertexId,
        Action invalidateVisual)
    {
        var continuityMenu = new MenuItem()
        {
            Header = "Continuity"
        };
        
        var polygon = editor.Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var currentType = editor.GetVertexContinuity(polygonId, vertexId);
        
        foreach (var type in Enum.GetValues<ContinuityType>())
        {
            var item = new MenuItem
            {
                Header = type.ToString(),
                IsEnabled = currentType != type && ContinuityHelper.CanApply(polygon, vertexId, type),
                IsChecked = currentType == type
            };

            item.Click += (_, _) =>
            {
                if (editor.SetVertexContinuity(polygonId, vertexId, type))
                    invalidateVisual();
            };

            continuityMenu.Items.Add(item);
        }

        return continuityMenu;
    }

    private static MenuItem CreateDeleteItem(
        EditorService editor,
        Guid polygonId, Guid vertexId,
        Action invalidateVisual)
    {
        var deleteItem = new MenuItem
        {
            Header = "Delete vertex"
        };

        deleteItem.Click += (_, _) =>
        {
            if (editor.RemoveVertex(polygonId, vertexId))
                invalidateVisual();
        };

        return deleteItem;
    }
}