using System.Windows;

namespace PSD2PNG;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Current.MainWindow = new MainWindow(e.Args.FirstOrDefault());
        Current.MainWindow.Show();
    }
}