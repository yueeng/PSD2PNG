using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Appearance;

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

    public ICommand SwitchThemeCommand { get; } = new RelayCommand(() => ApplicationThemeManager.Apply(ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ? ApplicationTheme.Dark : ApplicationTheme.Light));
}