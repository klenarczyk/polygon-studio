using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PolygonStudio.App.Controls.Canvas;

public sealed class FixedEdgeLengthDialog : Window
{
    public double? AcceptedLength { get; private set; }

    public FixedEdgeLengthDialog(double currentLength)
    {
        Title = "Fix edge length";
        Width = 320;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var lengthInput = new TextBox
        {
            Text = currentLength.ToString("G", CultureInfo.CurrentCulture),
            MinWidth = 240,
            Margin = new Thickness(0, 6, 0, 12)
        };

        var errorText = new TextBlock
        {
            Foreground = Brushes.Firebrick,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var applyButton = new Button
        {
            Content = "Apply",
            MinWidth = 80
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 80
        };

        applyButton.Click += (_, _) =>
        {
            if (!double.TryParse(
                    lengthInput.Text, NumberStyles.Float, 
                    CultureInfo.CurrentCulture, 
                    out double length) ||
                !double.IsFinite(length) ||
                length <= 0)
            {
                errorText.Text = "Enter a finite, positive length.";
                errorText.IsVisible = true;
                return;
            }

            AcceptedLength = length;
            Close(true);
        };

        cancelButton.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, applyButton }
        };

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Target edge length:" },
                lengthInput,
                errorText,
                buttons
            }
        };
    }
}