using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PolygonStudio.App.Dialogs;
using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services;
using LineGeometry = PolygonStudio.Core.Model.Geometries.LineGeometry;

namespace PolygonStudio.App.ContextMenus;

public static class EdgeContextMenu
{
    public static void Show(
        Control owner,
        EditorService editor,
        Guid polygonId, Guid edgeId,
        EdgeGeometry currentGeometry,
        Action invalidateVisual,
        bool canAddPerpendicularConstraint,
        Func<bool> addPerpendicularConstraint)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Pointer
        };

        var geometryMenu = CreateGeometryMenu(editor, polygonId, edgeId, currentGeometry, invalidateVisual);

        var constraintMenu = CreateConstraintMenu(
            owner,
            editor,
            polygonId, edgeId,
            currentGeometry,
            invalidateVisual,
            canAddPerpendicularConstraint,
            addPerpendicularConstraint
        );

        var splitItem = CreateSplitItem(editor, polygonId, edgeId, invalidateVisual);
        
        var removeMenu = CreateRemoveConstraintMenu(
            editor,
            polygonId,
            edgeId,
            invalidateVisual
        );
        
        constraintMenu.Items.Add(new Separator());
        constraintMenu.Items.Add(removeMenu);
        
        // ---
        
        menu.Items.Add(geometryMenu);
        menu.Items.Add(constraintMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(splitItem);
        
        menu.Open(owner);
    }
    
    // ---

    private static MenuItem CreateGeometryMenu(
        EditorService editor,
        Guid polygonId, Guid edgeId,
        EdgeGeometry currentGeometry,
        Action invalidateVisual)
    {
        var geometryMenu = new MenuItem()
        {
            Header = "Geometry"
        };
        
        AddGeometryItem("Line", typeof(LineGeometry));
        AddGeometryItem("Cubic Bezier", typeof(CubicBezierGeometry));
        AddGeometryItem("Circular arc", typeof(CircularArcGeometry));

        return geometryMenu;
        
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

    private static MenuItem CreateConstraintMenu(
        Control owner,
        EditorService editor,
        Guid polygonId, Guid edgeId,
        EdgeGeometry currentGeometry,
        Action invalidateVisual,
        bool canAddPerpendicularConstraint,
        Func<bool> addPerpendicularConstraint)
    {
        var constraintMenu = new MenuItem()
        {
            Header = "Constraints"
        };
        
        var existingFixedLength = editor.Scene.Constraints
            .OfType<FixedLengthConstraint>()
            .FirstOrDefault(c => c.PolygonId == polygonId && c.EdgeId == edgeId);

        var fixedLengthItem = new MenuItem
        {
            Header = "Fix edge length...",
            IsEnabled = currentGeometry is LineGeometry
        };

        fixedLengthItem.Click += async (_, _) =>
        {
            var polygon = editor.Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
            var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);

            if (polygon is null || edge is null)
                return;

            var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
            var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);

            if (from is null || to is null)
                return;

            double dx = to.Position.X - from.Position.X;
            double dy = to.Position.Y - from.Position.Y;
            double currentLength = Math.Sqrt(dx * dx + dy * dy);

            if (TopLevel.GetTopLevel(owner) is not Window ownerWindow)
                return;

            var dialog = new FixedLengthDialog(currentLength);
            await dialog.ShowDialog<bool>(ownerWindow);

            if (dialog.AcceptedLength is not { } length)
                return;

            if (existingFixedLength != null)
            {
                editor.UpdateFixedEdgeLengthConstraint(existingFixedLength, length);
                invalidateVisual();
            }
            else if (editor.AddFixedEdgeLengthConstraint(polygonId, edgeId, length))
            {
                invalidateVisual();
            }
            else
            {
                await ShowConstraintErrorAsync(ownerWindow);
            }
        };

        var perpendicularItem = new MenuItem
        {
            Header = "Perpendicular-ify",
            IsEnabled = canAddPerpendicularConstraint
        };

        perpendicularItem.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(owner) is not Window ownerWindow)
                return;
            
            if (addPerpendicularConstraint())
            {
                invalidateVisual();
            }else
            {
                await ShowConstraintErrorAsync(ownerWindow);
            }
        };
        
        constraintMenu.Items.Add(fixedLengthItem);
        constraintMenu.Items.Add(perpendicularItem);

        return constraintMenu;
    }

    private static MenuItem CreateSplitItem(
        EditorService editor,
        Guid polygonId, Guid edgeId,
        Action invalidateVisual)
    {
        var splitItem = new MenuItem
        {
            Header = "Split edge"
        };

        splitItem.Click += (_, _) =>
        {
            if (editor.SplitEdge(polygonId, edgeId))
                invalidateVisual();
        };

        return splitItem;
    }
    
    private static MenuItem CreateRemoveConstraintMenu(
        EditorService editor,
        Guid polygonId,
        Guid edgeId,
        Action invalidateVisual)
    {
        var removeMenu = new MenuItem
        {
            Header = "Remove constraint"
        };

        var constraints = editor.Scene.Constraints
            .Where(c =>
                (c is FixedLengthConstraint fixedLength &&
                 fixedLength.PolygonId == polygonId &&
                 fixedLength.EdgeId == edgeId) ||
                (c is PerpendicularConstraint perpendicular &&
                 (
                     (perpendicular.PolygonId1 == polygonId &&
                      perpendicular.EdgeId1 == edgeId)
                     ||
                     (perpendicular.PolygonId2 == polygonId &&
                      perpendicular.EdgeId2 == edgeId)
                 )))
            .ToList();

        foreach (var constraint in constraints)
        {
            var item = new MenuItem
            {
                Header = constraint switch
                {
                    FixedLengthConstraint => "Fixed length",
                    PerpendicularConstraint => "Perpendicular",
                    _ => constraint.GetType().Name
                }
            };

            item.Click += (_, _) =>
            {
                if (editor.RemoveConstraint(constraint.Id))
                    invalidateVisual();
            };

            removeMenu.Items.Add(item);
        }

        removeMenu.IsEnabled = removeMenu.Items.Count > 0;

        return removeMenu;
    }
    
    // ---
    
    private static async Task ShowConstraintErrorAsync(Window owner)
    {
        var dialog = new Window
        {
            Title = "Constraint not applied",
            Width = 340,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "The constraint could not be applied.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Right,
                        MinWidth = 70
                    }
                }
            }
        };

        if (dialog.Content is StackPanel panel &&
            panel.Children.OfType<Button>().FirstOrDefault() is { } button)
        {
            button.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(owner);
    }
}