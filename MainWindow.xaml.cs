﻿using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
    public MainWindow()
    {
        InitializeComponent();
        Unloaded += WindowOnUnloaded;
    }

    public MainViewModel ConcreteDataContext => (MainViewModel)DataContext;

    private void PSDOnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        ConcreteDataContext.PSDPath = string.Empty;
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
        set
        {
            SetProperty(ref _pngPath, value);
            OnPropertyChanged(nameof(SaveCommand));
        }
    }

    #endregion

    public ICommand SaveCommand => new RelayCommand(() =>
    {
        var save = new SaveFileDialog
        {
            FileName = Path.GetFileNameWithoutExtension(PSDPath),
            Filter = "PNG|*.png"
        };
        if (save.ShowDialog() != true) return;
        File.Copy(PNGPath, save.FileName, true);
    }, () => !string.IsNullOrEmpty(PNGPath));

    public async Task Transform()
    {
        if (string.IsNullOrEmpty(PSDPath)) return;
        if (!File.Exists(PSDPath)) return;
        try
        {
            var (c, o) = await Cmd("magick", $@"""{PSDPath}"" -format ""%[compose],"" info:");
            if (c != 0) throw new Exception($"magick: {c}\n{o}");
            var layers = o.TrimEnd(',').Split(',');
            if (layers.Length == 0) throw new Exception($"magick: {c}\n{o}");
            var folder = Path.Combine(Folder, Guid.NewGuid().ToString());
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var preview = Path.Combine(folder, "preview.png");
            (c, o) = await Cmd("magick", $@"convert -depth 24 ""{PSDPath}[0]"" ""{preview}""");
            if (c != 0) throw new Exception($"convert[0]: {c}\n{o}");
            PreviewPath = preview;
            foreach (var (it, i) in layers.Select((it, i) => (it, i)))
            {
                if (i == 0) continue;
                if (it != "Over") continue;
                var target = Path.Combine(folder, $"output-{i}.png");
                (c, o) = await Cmd("magick", $@"convert -depth 24 ""{PSDPath}[{i}]"" ""{target}""");
                if (c != 0) throw new Exception($"convert[{i}]: {c}\n{o}");
            }

            var q = Math.Sqrt(layers.Count(l => l == "Over") - 1);
            var w = (int)Math.Ceiling(q);
            var h = (int)Math.Floor(q);
            var output = Path.Combine(folder, "target.png");
            (c, o) = await Cmd("magick", $@"montage -depth 24 -background none -geometry +10+10 -tile {w}x{h} -set label "" "" ""{Path.Combine(folder, "output-*.png")}"" ""{output}""");
            if (c != 0) throw new Exception($"montage: {c}\n{o}");
            PNGPath = output;
        }
        catch (Exception e)
        {
            _ = new MessageBox { Title = "转换错误", Content = e.Message }.ShowDialogAsync();
        }
    }

    public static async Task<(int, string)> Cmd(string cmd, string args)
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
            var output = await process.StandardOutput.ReadToEndAsync();
            return (process.ExitCode, output);
        }
        catch (Exception e)
        {
            await new MessageBox
            {
                Title = "出错",
                Content = e.Message
            }.ShowDialogAsync();
        }

        return (-1, string.Empty);
    }
}