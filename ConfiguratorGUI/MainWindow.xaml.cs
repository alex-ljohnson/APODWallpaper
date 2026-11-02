using APODWallpaper;
using APODWallpaper.Utils;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using SPath = System.Windows.Shapes.Path;
namespace ConfiguratorGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SPath pathNotMax = TitleBarIcon("M 0,0 H 8 V 8 H 0 Z");
        private readonly SPath pathMax = TitleBarIcon("M 0,2 H 8 V 10 H 0 Z M 2,2 V 0 H 10 V 8 H 8");

        private static SPath TitleBarIcon(string data)
        {
            var path = new SPath { Data = Geometry.Parse(data) };
            path.SetResourceReference(SPath.StrokeProperty, "Foreground");
            return path;
        }

        private readonly ViewModel VM;
        private readonly StdOutRedirect redirect;
        private readonly IAPODWallpaper APOD;
        private readonly Configuration Config;
        public MainWindow(IAPODWallpaper apod, ViewModel vm, Configuration config)
        {
            APOD = apod;
            VM = vm;
            Config = config;
            DataContext = vm;
            InitializeComponent();
            redirect = new StdOutRedirect(TxtOutput);
            Console.SetOut(redirect);
        }

        #region Window Control
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool closeBoardComplete = false;
        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Storyboard closeStory = (Storyboard)FindResource("closeAnim");
            if (!closeBoardComplete)
            {
                closeStory.Begin();
                e.Cancel = true;
            }
        }
        private async void window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            var startTime = DateTime.UtcNow;
            await VM.Initialise();
            Trace.WriteLine($"Init time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
            
            Trace.WriteLine("\nWINDOW LOADED\n");
            _ = Updater.CheckUpdate(true);

        }

        private void CloseStoryboard_Completed(object sender, EventArgs e)
        {
            closeBoardComplete = true;
            Close();
        }

        private void BtnMinimise_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximise_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            BtnMaximise.Content = WindowState == WindowState.Maximized ? pathMax : pathNotMax;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowProc);
        }

        // Borderless windows maximise past the screen edges and over the taskbar, so cap them to the work area
        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            const uint MONITOR_DEFAULTTONEAREST = 2;
            if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            // Relative to the monitor
            mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
            mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
            mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
            mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    BtnMaximise_Click(sender, e);
                }
                else if (e.ClickCount == 1)
                {
                    DragMove();
                }
            }
        }
        #endregion

        async private void BtnForceRun_Click(object sender, RoutedEventArgs e)
        {
            if (VM.IsUpdating) return;
            TxtOutput.Clear();
            OutputTab.Focus();
            VM.IsUpdating = true;
            try
            {
                await APOD.UpdateAsync(true);
            }
            finally
            {
                VM.IsUpdating = false;
            }
            Console.WriteLine("\nProcess Finished!\n");
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            await Updater.CheckUpdate();
        }
        // TODO: Move methods to viewmodel
        private async void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (!Configuration.DefaultConfiguration.isReady) await Configuration.DefaultConfiguration.InitialiseAsync();
            Config.CopyConfiguration(Configuration.DefaultConfiguration);
        }

        private void BtnClearOut_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Clear();
        }

        private void CmbConfiguratorTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyTheme();
        }

        private async void ApplyTheme()
        {
            if (CmbConfiguratorTheme.SelectedItem == null) { return; }
            await ((App)Application.Current).SetTheme();
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NotIntegerRegex();
        private void TxtPreviewQuality_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NotIntegerValidation(e.Text);
        }

        private void TxtNetworkTimeout_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NotIntegerValidation(e.Text);
        }

        private static bool NotIntegerValidation(string text)
        {
            Regex regex = NotIntegerRegex();
            return regex.IsMatch(text);
        }
        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (NotIntegerValidation(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void BtnStyleChange_Click(object sender, RoutedEventArgs e)
        {
            APOD.UpdateBackground(style: (WallpaperStyleEnum)Config.WallpaperStyle);
        }

        private void BtnExploreFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", Environment.ExpandEnvironmentVariables("%appdata%\\APODWallpaper\\images"));
        }

        private void KeySetupOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Focus once layout has run, keeps Tab and Esc inside the panel
            if ((bool)e.NewValue)
                Dispatcher.BeginInvoke(() => TxtKeySetupInput.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BtnOpenHelp_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(ViewModel.HelpPath) { UseShellExecute=true});
        }

        private void BitmapImage_DownloadCompleted(object sender, EventArgs e)
        {
            //var img = sender as BitmapImage;
            //Trace.WriteLine($"Image downloaded: {img?.Format} {img?.Metadata} ");
        }
    }
}
