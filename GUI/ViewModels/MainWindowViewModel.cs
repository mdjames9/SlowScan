using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public Bitmap MyBitmap 
    { 
        get => _MyBitmap; 
        set 
        {
            if(_MyBitmap != value)
            {
                _MyBitmap = value;
                OnPropertyChanged(nameof(MyBitmap));
            }
        }
    }

    private Bitmap _MyBitmap;
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MainWindowViewModel()
    {
        mainCanvas = new(256, 256, true);
        _MyBitmap = SKBitmapToAvaloniaBitmap(mainCanvas);
    }

    public Avalonia.Media.Imaging.Bitmap SKBitmapToAvaloniaBitmap(SKBitmap skBitmap)
    {
        SKData data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
        using (Stream stream = data.AsStream())
        {
            return new Avalonia.Media.Imaging.Bitmap(stream);
        }
    }

    SKBitmap mainCanvas;

    public void SetImageDimensions((int, int) dim)
    {
        mainCanvas = new SKBitmap(dim.Item1, dim.Item2, true);
    }
    public void DoBitmapStuff((int, byte[], byte[], byte[]) lineData)
    {
        for(int i = 0; i < lineData.Item2.Length; i++)
        {
            mainCanvas.SetPixel(i, lineData.Item1, new SKColor(lineData.Item2[i], lineData.Item3[i], lineData.Item4[i]));
        }
        Bitmap bmp = SKBitmapToAvaloniaBitmap(mainCanvas);
        MyBitmap = bmp;
    }

}
