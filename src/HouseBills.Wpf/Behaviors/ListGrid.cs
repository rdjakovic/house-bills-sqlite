using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HouseBills.Wpf.Behaviors;

/// <summary>
/// List shortcuts for a read-only <see cref="DataGrid"/>: double-click or Enter on a row runs <c>OpenCommand</c>,
/// Delete runs <c>DeleteCommand</c>. Pure UI wiring; the commands live in the view model.
/// </summary>
public static class ListGrid
{
    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.RegisterAttached(
        "OpenCommand", typeof(ICommand), typeof(ListGrid), new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.RegisterAttached(
        "DeleteCommand", typeof(ICommand), typeof(ListGrid), new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetOpenCommand(DependencyObject element) => (ICommand?)element.GetValue(OpenCommandProperty);

    public static void SetOpenCommand(DependencyObject element, ICommand? value) => element.SetValue(OpenCommandProperty, value);

    public static ICommand? GetDeleteCommand(DependencyObject element) => (ICommand?)element.GetValue(DeleteCommandProperty);

    public static void SetDeleteCommand(DependencyObject element, ICommand? value) => element.SetValue(DeleteCommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            return;
        }

        // Unsubscribe first so setting both properties doesn't subscribe twice.
        grid.MouseDoubleClick -= OnMouseDoubleClick;
        grid.PreviewKeyDown -= OnPreviewKeyDown;
        grid.MouseDoubleClick += OnMouseDoubleClick;
        grid.PreviewKeyDown += OnPreviewKeyDown;
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;

        // Only on a row: not on column headers (which sort) or scroll bars.
        if (e.ChangedButton != MouseButton.Left
            || e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(grid, source) is not DataGridRow)
        {
            return;
        }

        e.Handled = TryExecute(GetOpenCommand(grid));
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var grid = (DataGrid)sender;
        if (Keyboard.Modifiers != ModifierKeys.None || grid.SelectedItem is null)
        {
            return;
        }

        // Preview: the DataGrid itself would otherwise use Enter to move to the next row.
        var command = e.Key switch
        {
            Key.Enter => GetOpenCommand(grid),
            Key.Delete => GetDeleteCommand(grid),
            _ => null,
        };
        e.Handled = TryExecute(command);
    }

    private static bool TryExecute(ICommand? command)
    {
        if (command?.CanExecute(null) != true)
        {
            return false;
        }

        command.Execute(null);
        return true;
    }
}