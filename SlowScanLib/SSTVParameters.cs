public class SSTVParameters
{
    
    public double LineLengthMS {get; set;}

    public int PixelsPerLine {get; set;}

    public int LinesPerFrame {get; set;}

    public double SyncLengthMS {get; set;}

    public double StepSize {get; set;}

    public static int SamplesPerMS {get; set;} = 48;

    public double BlackLevelTimeMS {get; set;}
    
    public bool RGB { get; set;}

    public bool SyncAfterGreen {get; set;}

    public SSTVParameters(double lineLength, double syncLengthMs, double blackLevelLengthMs, int pixelWidth, int lines, bool rgb, bool syncAfterGreen = false)
    {
        LinesPerFrame = lines;
        LineLengthMS = lineLength;
        PixelsPerLine = pixelWidth;
        SyncLengthMS = syncLengthMs;
        BlackLevelTimeMS = blackLevelLengthMs;
        RGB = rgb;
        SyncAfterGreen = syncAfterGreen;
        StepSize =  (LineLengthMS * SamplesPerMS)/ (pixelWidth);
    }
}