using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace PSD2PNG;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow() => InitializeComponent();

    public MainViewModel ConcreteDataContext => (MainViewModel)DataContext;

    private void PSDOnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        ConcreteDataContext.PSDPath = files[0];
    }

    private void WindowOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(MainViewModel.Folder)) return;
        Directory.Delete(MainViewModel.Folder, true);
    }
}

public class MainViewModel : ObservableObject
{
    public static string Folder => Path.Combine(Path.GetTempPath(), "PSD2PNG");

    #region PSDPath

    private string _psdPath;

    public string PSDPath
    {
        get => _psdPath;
        set
        {
            if (!SetProperty(ref _psdPath, value)) return;
            _ = Transform();
        }
    }

    #endregion

    #region PreviewPath

    private string _previewPath;

    public string PreviewPath
    {
        get => _previewPath;
        set => SetProperty(ref _previewPath, value);
    }

    #endregion

    #region PNGPath

    private string _pngPath;

    public string PNGPath
    {
        get => _pngPath;
        set => SetProperty(ref _pngPath, value);
    }

    #endregion

    #region Height

    private int _height = 240;

    public int Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    #endregion

    public ICommand TransformCommand => new AsyncRelayCommand(async () => await Transform());

    public async Task Transform()
    {
        if (string.IsNullOrEmpty(PSDPath)) return;
        if (!File.Exists(PSDPath)) return;
        var folder = Path.Combine(Folder, Guid.NewGuid().ToString());
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "output.png");
        await Magick($"-depth 24 \"{PSDPath}\" \"{target}\"");
        var pngs = Directory.GetFiles(folder, "*.png");
        PreviewPath = pngs[0];
        var q = Math.Sqrt(pngs.Length);
        var w = (int)Math.Ceiling(q);
        await Montage($"-geometry +10+10 -tile {w}x{w} \"{Path.Combine(folder, "output*.png")}\" \"{target}\"");
        PNGPath = target;
    }

    public static async Task<int> Magick(string args) => await Run("magick", args);

    public static async Task<int> Montage(string args) => await Run("montage", args);

    public static async Task<int> Run(string cmd, string args)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = cmd;
            process.StartInfo.Arguments = args;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            await process.StandardOutput.ReadToEndAsync();
            return process.ExitCode;
        }
        catch (Exception e)
        {
            await new MessageBox
            {
                Title = "出错",
                Content = e.Message
            }.ShowDialogAsync();
        }

        return -1;
    }
}