using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PolygonStudio.App.Dialogs;

public partial class FixedLengthDialog : Window
{
    public double? AcceptedLength { get; private set; }
    
    public FixedLengthDialog(double currentLength)
    {
        InitializeComponent();
        LengthInput.Text = currentLength.ToString("F");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        AcceptedLength = null;
        Close();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        string? text = LengthInput.Text;

        if (!double.TryParse(text, out double length) || length <= 1e-12)
        {
            ErrorText.Text = "Enter a length greater than zero.";
            ErrorText.IsVisible = true;
            return;
        }
        
        AcceptedLength = length;
        Close();
    }
}