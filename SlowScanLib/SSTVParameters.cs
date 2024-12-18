public class SSTVParameters
{
    
    public double LineLengthMS {get; set;}

    public int SamplesPerLine {get; set;}

    public int PixelsPerLine {get; set;}

    public int LinesPerFrame {get; set;}

    public double SyncLengthMS {get; set;}

    public int StepSize {get; set;}

    public static int SamplesPerMS {get; set;} = 48;

    public SSTVParameters(double lineLength, double syncLengthMs, int pixelWidth, int lines)
    {
        LinesPerFrame = lines;
        LineLengthMS = lineLength;
        PixelsPerLine = pixelWidth;
        SyncLengthMS = syncLengthMs;
        SamplesPerLine = (int)(SamplesPerMS * LineLengthMS);
        StepSize =  (int)((LineLengthMS * SamplesPerMS)/ (pixelWidth));

    }
}