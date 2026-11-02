using System.Windows;
using System.Windows.Threading;

namespace GameReviews;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandled;
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Ui.Error(e.Exception);
        e.Handled = true;
    }
}
