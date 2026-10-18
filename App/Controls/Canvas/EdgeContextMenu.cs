using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PolygonStudio.Core.Constraints;
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
        
        bool existingFixedLength = editor.Scene.Constraints
            .OfType<FixedEdgeLengthConstraint>()
            .Any(c => c.PolygonId == polygonId && c.EdgeId == edgeId);

        var fixedLengthItem = new MenuItem
        {
            Header = existingFixedLength
                ? "Fixed edge length already set"
                : "Fix edge length...",
            IsEnabled = !existingFixedLength
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

            var dialog = new FixedEdgeLengthDialog(currentLength);
            await dialog.ShowDialog<bool>(ownerWindow);

            if (dialog.AcceptedLength is not { } length)
                return;

            if (editor.AddFixedEdgeLengthConstraint(polygonId, edgeId, length))
            {
                invalidateVisual();
            }
            else // Constraint can't be applied
            {
                var errorDialog = new Window
                {
                    Title = "Constraint not applied",
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
                                Text = "The fixed-length constraint could not be applied."
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

                if (errorDialog.Content is StackPanel panel &&
                    panel.Children.OfType<Button>().FirstOrDefault() is { } okButton)
                {
                    okButton.Click += (_, _) => errorDialog.Close();
                }

                await errorDialog.ShowDialog(ownerWindow);
            }
        };

        constraintMenu.Items.Add(fixedLengthItem);
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