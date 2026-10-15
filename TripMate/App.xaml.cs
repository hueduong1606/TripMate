using System.Windows;
using System.Windows.Threading;

namespace TripMate
{
    // Interaction logic for App.xaml
    public partial class App : Application
    {
        public App()
        {
            // Last line of defence: if any error is not caught by a try/catch,
            // show a friendly message instead of letting the app crash
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        // Runs when an error was not caught anywhere else
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Something went wrong: " + e.Exception.Message + "\n\nYour data has not been lost. Please try again.",
                "TripMate", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}