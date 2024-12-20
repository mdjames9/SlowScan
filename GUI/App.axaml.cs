using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using GUI.ViewModels;
using GUI.Views;
using System;
using System.Threading.Tasks;
using SkiaSharp;

namespace GUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public MainWindowViewModel? _Main;
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            _Main = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = _Main,
            };
            desktop.MainWindow.Loaded += Loaded;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Loaded(object? sender, EventArgs e)
    {
        Task.Run(()=>{ReadFile();});
    }

    public void ReadFile()
    {
        Console.WriteLine("Hello, World!");

        SlowScan.WavReader wr = new SlowScan.WavReader();
        wr.Open("../../../../test/pd180test.wav");

        Machine m = new Machine(wr);
        m.SignalFound += SignalFound;
        m.LineAvailable += LineAvailable;
        m.Run();
    }
    private void SignalFound(object? sender, (int, int) dim)
    {
        _Main?.SetImageDimensions(dim);
    }

    private void LineAvailable(object? sender, (int, byte[], byte[], byte[]) lineData)
    {
        _Main?.DoBitmapStuff(lineData);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}