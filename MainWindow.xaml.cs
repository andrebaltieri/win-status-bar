using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;
using Microsoft.Win32;

namespace StatusBar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        // AppBar messages
        private const int ABM_NEW = 0x00000000;
        private const int ABM_REMOVE = 0x00000001;
        private const int ABM_QUERYPOS = 0x00000002;
        private const int ABM_SETPOS = 0x00000003;
        private const int ABE_TOP = 1;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private bool isDarkTheme = false;
        private IntPtr hwnd;
        private WinForms.Screen targetScreen;
        private System.Windows.Threading.DispatcherTimer clockTimer;
        private Settings settings;

        public MainWindow(WinForms.Screen screen)
        {
            InitializeComponent();

            targetScreen = screen;

            // Load settings
            settings = Settings.Load();

            // Read current Windows theme
            isDarkTheme = IsWindowsDarkTheme();

            // Initialize clock timer
            clockTimer = new System.Windows.Threading.DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += ClockTimer_Tick;
            clockTimer.Start();
            UpdateClock(); // Update immediately

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;
            StateChanged += MainWindow_StateChanged;
            ApplyTheme(isDarkTheme);
            ApplySettings();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // Prevent minimization - always keep window in normal state
            if (WindowState != WindowState.Normal)
            {
                WindowState = WindowState.Normal;
            }

            // Ensure window is always visible
            if (!IsVisible)
            {
                Show();
            }
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            // Force position back to top of target screen if it changes
            double targetLeft = targetScreen.Bounds.Left / GetDpiScale();
            double targetTop = targetScreen.Bounds.Top / GetDpiScale();

            if (Math.Abs(Left - targetLeft) > 1 || Math.Abs(Top - targetTop) > 1)
            {
                Left = targetLeft;
                Top = targetTop;
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Force width to match screen width
            double screenWidth = targetScreen.Bounds.Width / GetDpiScale();
            if (Math.Abs(Width - screenWidth) > 1)
            {
                Width = screenWidth;
            }
        }

        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;

            // Position window at top of target screen
            double dpiScale = GetDpiScale();
            Left = targetScreen.Bounds.Left / dpiScale;
            Top = targetScreen.Bounds.Top / dpiScale;
            Width = targetScreen.Bounds.Width / dpiScale;

            // Make window stay as topmost but not steal focus
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // Register as AppBar
            RegisterAppBar();

            // Prevent window from being moved
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_WINDOWPOSCHANGING = 0x0046;
            const int WM_MOVE = 0x0003;
            const int WM_MOVING = 0x0216;
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;
            const int WM_SHOWWINDOW = 0x0018;

            // Block minimize attempts (WIN+D, minimize button, etc)
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
            {
                handled = true;
                return IntPtr.Zero;
            }

            // Prevent hiding the window
            if (msg == WM_SHOWWINDOW && wParam == IntPtr.Zero)
            {
                handled = true;
                return IntPtr.Zero;
            }

            // Block any attempts to move or resize the window
            if (msg == WM_WINDOWPOSCHANGING || msg == WM_MOVE || msg == WM_MOVING)
            {
                // Keep window fixed at top of target screen
                double dpiScale = GetDpiScale();
                Left = targetScreen.Bounds.Left / dpiScale;
                Top = targetScreen.Bounds.Top / dpiScale;
                Width = targetScreen.Bounds.Width / dpiScale;
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Stop the clock timer
            clockTimer?.Stop();

            // Unregister AppBar when closing
            UnregisterAppBar();

            // Remove this window from the active windows list
            App.RemoveWindow(this);
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            ClockTextBlock.Text = DateTime.Now.ToString("h:mm tt");
        }

        private void RegisterAppBar()
        {
            var abd = new APPBARDATA
            {
                cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
                hWnd = hwnd,
                uCallbackMessage = 0
            };

            // Register the AppBar
            SHAppBarMessage(ABM_NEW, ref abd);

            // Set position and size for target screen
            abd.uEdge = ABE_TOP;
            abd.rc.left = targetScreen.Bounds.Left;
            abd.rc.top = targetScreen.Bounds.Top;
            abd.rc.right = targetScreen.Bounds.Right;
            abd.rc.bottom = targetScreen.Bounds.Top + (int)Height;

            // Query the system for an approved size and position
            SHAppBarMessage(ABM_QUERYPOS, ref abd);

            // Adjust the size and position
            abd.rc.bottom = abd.rc.top + (int)Height;

            // Set the final position
            SHAppBarMessage(ABM_SETPOS, ref abd);

            // Update window position (convert from pixels to WPF units)
            double dpiScale = GetDpiScale();
            Left = abd.rc.left / dpiScale;
            Top = abd.rc.top / dpiScale;
            Width = (abd.rc.right - abd.rc.left) / dpiScale;
            Height = (abd.rc.bottom - abd.rc.top) / dpiScale;
        }

        private void UnregisterAppBar()
        {
            var abd = new APPBARDATA
            {
                cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
                hWnd = hwnd
            };

            // Remove the AppBar
            SHAppBarMessage(ABM_REMOVE, ref abd);
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            isDarkTheme = !isDarkTheme;
            SetWindowsTheme(isDarkTheme);
            ApplyTheme(isDarkTheme);
        }

        private bool IsWindowsDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 0; // 0 = Dark, 1 = Light
                }
            }
            catch
            {
                // If we can't read the registry, default to light theme
            }
            return false;
        }

        private void SetWindowsTheme(bool useDarkTheme)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
                if (key != null)
                {
                    int themeValue = useDarkTheme ? 0 : 1; // 0 = Dark, 1 = Light
                    key.SetValue("AppsUseLightTheme", themeValue, RegistryValueKind.DWord);
                    key.SetValue("SystemUsesLightTheme", themeValue, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to change Windows theme: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyTheme(bool dark)
        {
            var resources = System.Windows.Application.Current.Resources;

            if (dark)
            {
                // Dark theme
                resources["StatusBarBackground"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
                resources["ButtonForeground"] = new SolidColorBrush(Colors.White);
                resources["ButtonHoverBackground"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60));
                ThemeToggleButton.Content = "☀️";
            }
            else
            {
                // Light theme
                resources["StatusBarBackground"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
                resources["ButtonForeground"] = new SolidColorBrush(Colors.Black);
                resources["ButtonHoverBackground"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220));
                ThemeToggleButton.Content = "🌙";
            }
        }

        private void ApplySettings()
        {
            // Show/Hide Clock
            ClockTextBlock.Visibility = settings.ShowClock ? Visibility.Visible : Visibility.Collapsed;

            // Show/Hide Theme button
            ThemeToggleButton.Visibility = settings.ShowTheme ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}