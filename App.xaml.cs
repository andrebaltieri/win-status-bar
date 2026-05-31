using System.Configuration;
using System.Data;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace StatusBar
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static List<MainWindow> activeWindows = new List<MainWindow>();
        private static readonly object windowLock = new object();

        public static IReadOnlyList<MainWindow> ActiveWindows
        {
            get
            {
                lock (windowLock)
                {
                    return activeWindows.AsReadOnly();
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Add global exception handler
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Load settings
            var settings = Settings.Load();

            // Create initial status bars
            CreateStatusBars(settings.ShowOnAllDisplays);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            System.Windows.MessageBox.Show($"A critical error occurred: {exception?.Message}\n\nStack trace:\n{exception?.StackTrace}", 
                "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void CreateStatusBars(bool showOnAllDisplays)
        {
            lock (windowLock)
            {
                // Store old windows
                var oldWindows = activeWindows.ToList();
                activeWindows.Clear();

                // Create new windows based on settings FIRST
                if (showOnAllDisplays)
                {
                    // Create a status bar for each screen
                    foreach (WinForms.Screen screen in WinForms.Screen.AllScreens)
                    {
                        var window = new MainWindow(screen);
                        activeWindows.Add(window);
                        window.Show();
                    }
                }
                else
                {
                    // Create a status bar only for the primary screen
                    var window = new MainWindow(WinForms.Screen.PrimaryScreen);
                    activeWindows.Add(window);
                    window.Show();
                }

                // Close old windows AFTER creating new ones
                foreach (var window in oldWindows)
                {
                    window.Close();
                }
            }
        }

        public static void RemoveWindow(MainWindow window)
        {
            lock (windowLock)
            {
                activeWindows.Remove(window);
            }
        }
    }

}
