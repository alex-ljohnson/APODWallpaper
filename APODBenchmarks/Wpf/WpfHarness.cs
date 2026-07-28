using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace APODBenchmarks.Wpf;

public sealed class WpfHarness : IDisposable
{
    private readonly Window window;
    private readonly ListView listView;
    private ScrollViewer? scrollViewer;

    public WpfHarness(PanelKind panel, TemplateKind template)
    {
        listView = new ListView
        {
            ItemsPanel = Templates.BuildPanelTemplate(panel),
            ItemTemplate = Templates.BuildItemTemplate(template),
        };
        ScrollViewer.SetCanContentScroll(listView, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(listView, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetScrollUnit(listView, ScrollUnit.Pixel);
        VirtualizingPanel.SetVirtualizationMode(listView, VirtualizationMode.Recycling);

        window = new Window
        {
            Width = 1000,
            Height = 500,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = listView,
        };
        window.Show();
        window.UpdateLayout();
    }

    public double Dpi => VisualTreeHelper.GetDpi(window).DpiScaleX;

    public void SetItems(IReadOnlyList<SyntheticItem>? items) => listView.ItemsSource = items;

    public void Layout()
    {
        listView.UpdateLayout();
        PumpDispatcher();
        scrollViewer ??= FindChild<ScrollViewer>(listView);
    }

    // no message loop here, so Background-priority cleanup never runs without a pump
    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    public double ViewportHeight => Sv().ViewportHeight;
    public double ScrollMax => Math.Max(0, Sv().ExtentHeight - Sv().ViewportHeight);

    public void ScrollTo(double offset)
    {
        Sv().ScrollToVerticalOffset(offset);
        listView.UpdateLayout();
        PumpDispatcher();
    }

    // realized container count = visual children of the items panel
    public int RealizedContainerCount()
    {
        var panel = FindChild<Panel>(listView, p =>
            p is ConfiguratorGUI.Controls.VirtualizingWrapPanel || p is WrapPanel);
        return panel is null ? 0 : VisualTreeHelper.GetChildrenCount(panel);
    }

    private ScrollViewer Sv() => scrollViewer ??= FindChild<ScrollViewer>(listView)
        ?? throw new InvalidOperationException("ScrollViewer not realized; call Layout first");

    private static T? FindChild<T>(DependencyObject root, Func<T, bool>? predicate = null)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && (predicate is null || predicate(t)))
                return t;
            var nested = FindChild(child, predicate);
            if (nested is not null) return nested;
        }
        return null;
    }

    public void Dispose() => window.Close();
}
