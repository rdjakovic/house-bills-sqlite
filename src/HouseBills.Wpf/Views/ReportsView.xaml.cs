using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HouseBills.Wpf.Views;

public partial class ReportsView : UserControl
{
    // View width below which the month and category sections stack instead of sitting side by side.
    private const double NarrowLayoutMaxWidth = 900;

    public ReportsView()
    {
        InitializeComponent();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
        {
            return;
        }

        var state = e.NewSize.Width < NarrowLayoutMaxWidth ? "Narrow" : "Wide";
        VisualStateManager.GoToState(this, state, useTransitions: false);
    }

    // When stacked, the tables are laid out at full height, but their own scroll viewers still
    // swallow the mouse wheel; scroll the page instead so the wheel works anywhere over the sections.
    private void OnSectionsScrollPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (SectionsScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            return;
        }

        SectionsScroll.ScrollToVerticalOffset(SectionsScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}