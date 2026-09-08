using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PortManager.Controls;

/// <summary>Stacks a grid's cells in reading order below its content-width breakpoint.</summary>
public static class AdaptiveLayout
{
    public static readonly DependencyProperty CompactBelowProperty = DependencyProperty.RegisterAttached(
        "CompactBelow", typeof(double), typeof(AdaptiveLayout), new PropertyMetadata(0d, OnChanged));
    public static double GetCompactBelow(DependencyObject obj) => (double)obj.GetValue(CompactBelowProperty);
    public static void SetCompactBelow(DependencyObject obj, double value) => obj.SetValue(CompactBelowProperty, value);

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Grid grid || (double)args.OldValue != 0) return;
        State? state = null;
        grid.Loaded += (_, _) => { state ??= new State(grid); state.Update(GetCompactBelow(grid)); };
        grid.SizeChanged += (_, _) => state?.Update(GetCompactBelow(grid));
    }

    private sealed class State
    {
        private readonly Grid _grid;
        private readonly ColumnDefinition[] _columns;
        private readonly RowDefinition[] _rows;
        private readonly (FrameworkElement Element, int Row, int Column, int RowSpan, int ColumnSpan)[] _cells;
        private bool _compact;

        public State(Grid grid)
        {
            _grid = grid;
            _columns = grid.ColumnDefinitions.ToArray();
            _rows = grid.RowDefinitions.ToArray();
            _cells = grid.Children.OfType<FrameworkElement>()
                .Select(e => (Element: e, Row: Grid.GetRow(e), Column: Grid.GetColumn(e),
                    RowSpan: Grid.GetRowSpan(e), ColumnSpan: Grid.GetColumnSpan(e)))
                .OrderBy(c => c.Row).ThenBy(c => c.Column).ToArray();
        }

        public void Update(double breakpoint)
        {
            var compact = _grid.ActualWidth < breakpoint;
            if (_compact == compact) return;
            _compact = compact;
            _grid.ColumnDefinitions.Clear();
            _grid.RowDefinitions.Clear();
            if (compact)
            {
                _grid.ColumnDefinitions.Add(new ColumnDefinition());
                for (var i = 0; i < _cells.Length; i++)
                {
                    _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var element = _cells[i].Element;
                    Grid.SetRow(element, i);
                    Grid.SetColumn(element, 0);
                    Grid.SetRowSpan(element, 1);
                    Grid.SetColumnSpan(element, 1);
                }
            }
            else
            {
                foreach (var column in _columns) _grid.ColumnDefinitions.Add(column);
                foreach (var row in _rows) _grid.RowDefinitions.Add(row);
                foreach (var cell in _cells)
                {
                    Grid.SetRow(cell.Element, cell.Row);
                    Grid.SetColumn(cell.Element, cell.Column);
                    Grid.SetRowSpan(cell.Element, cell.RowSpan);
                    Grid.SetColumnSpan(cell.Element, cell.ColumnSpan);
                }
            }
        }
    }
}
