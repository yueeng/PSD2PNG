using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace PSD2PNG;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow(string? psd = null)
    {
        InitializeComponent();
        Loaded += WindowOnLoaded;
        ConcreteDataContext.PSDPath = psd;
    }

    public MainViewModel ConcreteDataContext => (MainViewModel)DataContext;

    private void WindowOnLoaded(object sender, RoutedEventArgs e) => _ = MainViewModel.CheckImageMagick();

    private void PSDOnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        ConcreteDataContext.PSDPath = files[0];
    }
}

public class MainViewModel : ObservableObject
{
    public static string Folder => Path.Combine(Path.GetTempPath(), "PSD2PNG");

    #region Busy

    private bool _busy;

    public bool Busy
    {
        get => _busy;
        set
        {
            SetProperty(ref _busy, value);
            OnPropertyChanged(nameof(TransformCommand));
        }
    }

    #endregion

    #region PSDPath

    private string? _psdPath;

    public string? PSDPath
    {
        get => _psdPath;
        set
        {
            SetProperty(ref _psdPath, value);
            OnPropertyChanged(nameof(TransformCommand));
            _ = Transform();
        }
    }

    #endregion

    #region PreviewPath

    private string? _previewPath;

    public string? PreviewPath
    {
        get => _previewPath;
        set
        {
            SetProperty(ref _previewPath, value);
            OnPropertyChanged(nameof(SavePreviewCommand));
        }
    }

    #endregion

    #region PNGPath

    private string? _pngPath;

    public string? PNGPath
    {
        get => _pngPath;
        set
        {
            SetProperty(ref _pngPath, value);
            OnPropertyChanged(nameof(SaveCommand));
        }
    }

    #endregion

    #region Geometry

    private int _geometry = 10;

    public int Geometry
    {
        get => _geometry;
        set => SetProperty(ref _geometry, value);
    }

    #endregion

    public ICommand CleanupCommand => new AsyncRelayCommand(async () =>
    {
        try
        {
            PreviewPath = null;
            PNGPath = null;
            PSDPath = null;
            await Task.Yield();
            if (!Directory.Exists(Folder)) return;
            Directory.Delete(Folder, true);
        }
        catch
        {
            // ignored
        }
    });

    private static void Save(string source, string name, string ex = "")
    {
        var save = new SaveFileDialog
        {
            InitialDirectory = Path.GetDirectoryName(name),
            FileName = Path.GetFileNameWithoutExtension(name) + ex,
            Filter = "PNG|*.png"
        };
        if (save.ShowDialog() != true) return;
        File.Copy(source, save.FileName, true);
    }

    public ICommand SavePreviewCommand => new RelayCommand(() => Save(PreviewPath!, PSDPath!, " - preview"), () => !string.IsNullOrEmpty(PreviewPath));

    public ICommand SaveCommand => new RelayCommand(() => Save(PNGPath!, PSDPath!, " - flatten"), () => !string.IsNullOrEmpty(PNGPath));

    public ICommand TransformCommand => new RelayCommand(() => _ = Transform(), () => !string.IsNullOrEmpty(PSDPath) && !Busy);

    public ICommand OpenCommand => new RelayCommand(() =>
    {
        if (Busy)
        {
            _token?.Cancel();
            return;
        }

        var open = new OpenFileDialog { Filter = "PSD|*.psd" };
        if (open.ShowDialog() != true) return;
        PSDPath = open.FileName;
    });

    private CancellationTokenSource? _token;

    public async Task Transform()
    {
        if (string.IsNullOrEmpty(PSDPath)) return;
        if (!File.Exists(PSDPath)) return;
        if (Busy)
        {
            _ = new MessageBox { Title = "转换错误", Content = "正在转换，请等待转换完成。" }.ShowDialogAsync(false);
            return;
        }

        var token = (_token = new()).Token;
        Busy = true;
        try
        {
            PreviewPath = null;
            PNGPath = null;
            await Task.Yield();

            #region 获取图层信息

            var (c, o) = await Cmd("magick", $@"""{PSDPath}"" -format ""%[compose]:%[width]x%[height],"" info:", token);
            if (c != 0) throw new Exception($"magick: {c}\n{o}");
            var regex = new Regex(@"^(\w+):(\d+)x(\d+)$");
            var layers = o.TrimEnd(',').Split(',')
                .Select(i => regex.Match(i))
                .Select((it, i) => (I: i, L: it.Success ? (O: it.Groups[1].Value == "Over", W: int.Parse(it.Groups[2].Value), H: int.Parse(it.Groups[3].Value)) : default))
                .ToList();
            if (layers.Count == 0) throw new Exception($"magick: {c}\n{o}");

            #endregion

            #region 导出图层

            token.ThrowIfCancellationRequested();
            var folder = Path.Combine(Folder, Guid.NewGuid().ToString());
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var preview = Path.Combine(folder, "preview.png");
            (c, o) = await Cmd("magick", $@"convert ""{PSDPath}[0]"" ""{preview}""", token);
            if (c != 0) throw new Exception($"convert[0]: {c}\n{o}");
            PreviewPath = preview;
            foreach (var (i, it) in layers)
            {
                token.ThrowIfCancellationRequested();
                if (i == 0) continue;
                if (!it.O) continue;
                var target = Path.Combine(folder, $"output-{i}.png");
                (c, o) = await Cmd("magick", $@"convert ""{PSDPath}[{i}]"" ""{target}""", token);
                if (c != 0) throw new Exception($"convert[{i}]: {c}\n{o}");
            }

            #endregion

            #region 合并PNG

            token.ThrowIfCancellationRequested();
            var output = Path.Combine(folder, "target.png");
            // var q = Math.Sqrt(layers.Count(l => l.Item2.O) - 1);
            // var w = (int)Math.Ceiling(q);
            // var h = (int)Math.Floor(q);
            // (c, o) = await Cmd("magick", $@"montage -background none -geometry +10+10 -tile {w}x{h} -set label "" "" ""{Path.Combine(folder, "output-*.png")}"" ""{output}""");
            // if (c != 0) throw new Exception($"montage: {c}\n{o}");

            var geometry = Geometry;
            var overlay = layers.Where(i => i.I != 0 && i.L.O).ToDictionary(i => new Packer.Box(i.L.W + geometry * 2, i.L.H + geometry * 2), i => i);
            var packer = new Packer();
            packer.AddBox(overlay.Keys.ToArray());
            packer.Fit(Packer.FitType.MaxSide);

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                foreach (var i in overlay)
                {
                    var overlayImage = new BitmapImage(new Uri(Path.Combine(folder, $"output-{i.Value.I}.png"), UriKind.Relative));
                    drawingContext.DrawImage(overlayImage, new Rect(i.Key.Fit!.X + geometry, i.Key.Fit.Y + geometry, overlayImage.PixelWidth, overlayImage.PixelHeight));
                }
            }

            var baseImage = new RenderTargetBitmap((int)packer.Root!.W, (int)packer.Root.H, 96, 96, PixelFormats.Pbgra32);
            baseImage.Render(drawingVisual);

            #region Save the RenderTargetBitmap as a PNG file

            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(baseImage));
            await using (var stream = new FileStream(output, FileMode.CreateNew))
            {
                pngEncoder.Save(stream);
            }

            #endregion

            PNGPath = output;

            #endregion
        }
        catch when (token.IsCancellationRequested)
        {
            // ignored
        }
        catch (Exception e)
        {
            _ = new MessageBox { Title = "转换错误", Content = e.Message }.ShowDialogAsync(false, token);
        }
        finally
        {
            if (token == _token.Token) Busy = false;
        }
    }

    public static async Task CheckImageMagick()
    {
        try
        {
            var (c, o) = await Cmd("magick", "--version", default);
            if (c != 0) throw new Exception($"本地未安装 ImageMagick, 推荐使用 scoop install imagemagick 安装\n{o}");
        }
        catch (Exception e)
        {
            _ = new MessageBox { Title = "环境错误", Content = e.Message }.ShowDialogAsync(false);
        }
    }

    public static async Task<(int, string)> Cmd(string cmd, string args, CancellationToken token)
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
            await process.WaitForExitAsync(token);
            var output = await process.StandardOutput.ReadToEndAsync(token);
            return (process.ExitCode, output);
        }
        catch (Exception e)
        {
            return (-1, e.Message);
        }
    }
}