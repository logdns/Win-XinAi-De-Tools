using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace PortManager.Controls;

/// <summary>Wraps toolbars using measured control widths, including translated labels.</summary>
public sealed class WrapPanel : Panel
{
    public double Spacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize) => Layout(availableSize.Width, false);
    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, true);
        return finalSize;
    }

    private Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            if (!arrange) child.Measure(new Size(width, double.PositiveInfinity));
            var size = child.DesiredSize;
            var childWidth = Math.Min(width, size.Width);
            if (x > 0 && x + childWidth > width)
            {
                x = 0;
                y += rowHeight + Spacing;
                rowHeight = 0;
            }
            if (arrange) child.Arrange(new Rect(x, y, childWidth, size.Height));
            usedWidth = Math.Max(usedWidth, x + childWidth);
            x += childWidth + Spacing;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return new Size(usedWidth, y + rowHeight);
    }
}
