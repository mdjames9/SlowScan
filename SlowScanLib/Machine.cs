using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using DtmfDetection;
using SlowScan;

public class Machine
{
    public const int LeaderTone1 = 1900;
    public const int BreakTone = 1200;
    public const int HSYNCTone = 1500;  
    public const int OneTone = 1100;
    public const int ZeroTone = 1300;

    public int SampleRate {get; set;}
    public int SamplesPerMS {get; set;}

    public int BlockSize {get; set;}
    public Goertzel[] ToneFilters;


    public Machine(WavReader reader)
    {
        uint sampleRate = reader.GetSampleRate();
        mySamples = reader.GetSamples().ToArray();

        SampleRate = (int)sampleRate;
        SamplesPerMS = (int)(sampleRate/1000);
        SSTVParameters.SamplesPerMS = SamplesPerMS;   
        BlockSize = SamplesPerMS * 10; // Block size should be about 10 ms.

        ToneFilters = new Goertzel[]
        {
            Goertzel.Init(800, SampleRate, BlockSize),
            Goertzel.Init(OneTone, SampleRate, BlockSize),
            Goertzel.Init(BreakTone, SampleRate, BlockSize),
            Goertzel.Init(ZeroTone, SampleRate, BlockSize),
            Goertzel.Init(HSYNCTone, SampleRate, BlockSize),
            Goertzel.Init(LeaderTone1, SampleRate, BlockSize),
            Goertzel.Init(2500, SampleRate, BlockSize)
        };

        ColorFilters = new Goertzel[]
        {
            Goertzel.Init(BreakTone, SampleRate, BlockSize),
            Goertzel.Init(1500, SampleRate, BlockSize),
            Goertzel.Init(1600, SampleRate, BlockSize),
            Goertzel.Init(1700, SampleRate, BlockSize),
            Goertzel.Init(1800, SampleRate, BlockSize),
            Goertzel.Init(1900, SampleRate, BlockSize),
            Goertzel.Init(2000, SampleRate, BlockSize),
            Goertzel.Init(2100, SampleRate, BlockSize),
            Goertzel.Init(2200, SampleRate, BlockSize),
            Goertzel.Init(2300, SampleRate, BlockSize),
        };
    }

    public bool FindTone(FilterType tone, int msLength, ushort[] samples, ref int startIndex)
    {
        // Sample the signal in tenMS chunks
        // The first leader tone is 300ms in length, so to find the proper alignment,
        // We'll check 45 chunks of data and pick the alignment/start time that works best.
        bool foundOptimalLeader = false;
        int optimalStartIndex = -1;
        CandidateVoter cv = new CandidateVoter(tone, msLength);
        cv.MatchFound +=   
        (s, e) =>
        {
            optimalStartIndex = e; foundOptimalLeader = true;
        };
        
        while(!foundOptimalLeader)
        {
            int endIndex = startIndex + BlockSize;
            if(endIndex > samples.Length)
            {
                throw new InvalidDataException("No SSTV Found.");
            }
            for(int i = startIndex; i < endIndex; i++)
            {
                foreach(FilterType index in Enum.GetValues<FilterType>())
                {
                    ToneFilters[(int)index] = ToneFilters[(int)index].AddSample(samples[i]);
                }
            }
            double confidence;
            FilterType currentMax = (FilterType)GetMaxFilter(ToneFilters, out confidence);
            ResetFilter(ToneFilters);
            cv.AddBlock(currentMax, startIndex);
            startIndex += BlockSize;

        }

        startIndex = optimalStartIndex + msLength * SamplesPerMS;
        return true;

    }

    private int GetMaxFilter(Goertzel[] filter, out double nextHighestMagnitude)
    {
        int maxType = -1;
        nextHighestMagnitude = 0;
        double maxMagnitude = 0;
        for(int i = 0; i < filter.Length; i++)
        {
            if(filter[i].Response > maxMagnitude)
            {
                maxType = i;
                maxMagnitude = filter[i].Response;
            }
        }

        double magnitudeOfLeft;
        double magnitudeOfRight;
        if (maxType == -1)
        {
            return  maxType;
        }
        if(maxType == 0)
        {
            magnitudeOfLeft = 0;
        }
        else
        {
            magnitudeOfLeft = filter[maxType - 1].Response;
        }
        if(maxType == filter.Length - 1)
        {
            magnitudeOfRight = 0;
        }
        else
        {
            magnitudeOfRight = filter[maxType + 1].Response;
        }
        nextHighestMagnitude = -(magnitudeOfLeft / maxMagnitude) + (magnitudeOfRight / maxMagnitude);
        return maxType;
    }

    private void ResetFilter(Goertzel[] filter)
    {
        for(int i = 0; i < filter.Length; i++)
        {
            filter[i] = filter[i].Reset();
        }
    }


    public void FindVISToneStart(ushort[] samples, ref int startIndex)
    {
        int toneBlockSize = SamplesPerMS * 30;
        Goertzel prevTone = Goertzel.Init(LeaderTone1, SampleRate, toneBlockSize);
        Goertzel start = Goertzel.Init(BreakTone, SampleRate, toneBlockSize);
        do
        {
            int endIndex = startIndex + toneBlockSize;
            for(int i = startIndex; i < endIndex; i++)
            {
                start = start.AddSample(samples[i]);
                prevTone = prevTone.AddSample(samples[i]);                  
            }
            if(prevTone.Response > start.Response)
            {
                // The previous tone is still hanging around.. move up a ms..
                Console.WriteLine("Lining up..");
                startIndex += SamplesPerMS;
                start = start.Reset();
                prevTone = prevTone.Reset();
                continue;
            }
            else
            {
                startIndex += toneBlockSize;
            }
            break;
        }while(startIndex < samples.Length);
    }

    public int FindVISTone(ushort[] samples, ref int startIndex)
    {
        int toneBlockSize = 30 * SamplesPerMS;
        Goertzel one = Goertzel.Init(OneTone, SampleRate, toneBlockSize);
        Goertzel zero = Goertzel.Init(ZeroTone, SampleRate, toneBlockSize);
        int endIndex = startIndex + toneBlockSize;
        for(int i = startIndex; i < endIndex; i++)
        {
            one = one.AddSample(samples[i]);
            zero = zero.AddSample(samples[i]);               
        }
        startIndex += toneBlockSize;
        if (zero.Response > one.Response)
        {
            return 0;
        }
        else
        {
            return 1;
        }
    }

    public (byte[], byte[], byte[]) ReadLine(SSTVParameters sstv, ushort[] samples, ref double startIndex)
    {
        // HSYNC is handled by the line method. It will cause color processing to restart.
        // Scottie would need to consume the intial pulse first.
        //startIndex += 200;
        byte[] blue;
        byte[] green;
        byte[] red;
        bool greenSync = true;
        bool redSync = false;
        bool blueSync = false;
        if(sstv.SyncAfterGreen)
        {
            greenSync = false;
            blueSync = true;
            // Also means there will be an initial 1200Hz tone on the first line of the frame.
        }

        try
        {
            //ConsumeHSync(sstv, samples, ref startIndex);
            green = ReadLineGoertzel(greenSync, sstv, samples, ref startIndex);
            blue = ReadLineGoertzel(blueSync, sstv, samples, ref startIndex);
            red = ReadLineGoertzel(redSync, sstv, samples, ref startIndex);
            //startIndex += (int)Math.Ceiling((sstv.SyncLengthMS * SamplesPerMS));

        }
        catch(SyncException)
        {
            red = new byte[sstv.PixelsPerLine];
            blue = new byte[sstv.PixelsPerLine];
            green = new byte[sstv.PixelsPerLine];
        }
        return (green, blue, red);
    }

    public Goertzel[] ColorFilters;
    public class SyncException : Exception
    {

    }

    public event EventHandler<(int, int)>? SignalFound;
    public event EventHandler<(int, byte[], byte[], byte[])>? LineAvailable;
    ushort[] mySamples;
    public void Run()
    {
        int index = 0;

        // Start looking for the leader tone.
        Console.WriteLine("Looking for leader tone");
        bool foundLeader = FindTone(FilterType.Leader, 300, mySamples, ref index);

        Console.WriteLine("Looking for break tone.");
        bool foundpause = FindTone(FilterType.Break, 10, mySamples, ref index);

        Console.WriteLine("Looking for second leader tone.");
        bool foundLeader2 = FindTone(FilterType.Leader, 300, mySamples, ref index);


        if(foundLeader && foundpause && foundLeader2)
        {
            //Start, 7 data, even parity, stop
            byte viscode = ReadVIS(mySamples, ref index);
            SSTVParameters stv = new(0, 0, 0, 0, 0, true);
            //Manual choice?
            stv= new SSTVParameters(183.040, 20, 0, 640, 480, false);
            if (viscode == 96)
            {
                stv = new SSTVParameters(183.040, 20, 0, 640, 480, false);
                Console.WriteLine("VIS indicates PD 180");
            }
            else if (viscode == 60)
            {
                // This is Scottie S1.
                stv = new SSTVParameters(138.240, 9, 1.5, 320, 256, true, false);
                Console.WriteLine("VIS indicates Scottie.");
            }
            else if (viscode == 44 || viscode == 32)
            {
                // Martin M1!
                stv = new SSTVParameters(146.432, 4.862, 0.572, 320, 256, true);
                Console.WriteLine("VIS Indicates Martin M1.");
            }
            else if (viscode == 40)
            {
                stv = new SSTVParameters(73.216, 4.862, 0.572, 320, 256, true);
                Console.WriteLine("VIS Indicates Martin M2.");
            }
            SignalFound?.Invoke(this, (stv.PixelsPerLine, stv.LinesPerFrame));

            double lineStart = index;

            // For Scottie, we need to consume the 1200hz tone at the start of the frame before we proceed.
            ConsumeSync(stv, mySamples, ref lineStart);

            for(int i = 0; i < stv.LinesPerFrame; i++)
            {

                (byte[] green, byte[] blue, byte[] red) g = ReadLine(stv, mySamples, ref lineStart);
                if(!stv.RGB)
                {
                    // PD modes are in 4:2:0, so there is another luminance ("Y") line.
                    byte[] y2 = ReadLineGoertzel(false, stv, mySamples, ref lineStart);
                    var rgb1 = ConvertYCbCrToRGB(g.green, g.blue, g.red);
                    var rgb2 = ConvertYCbCrToRGB(y2, g.blue, g.red);
                    LineAvailable?.Invoke(this, (i, rgb1.r, rgb1.g, rgb1.b));
                    LineAvailable?.Invoke(this, (++i, rgb2.r, rgb2.g, rgb2.b));
                }
                else
                {
                    LineAvailable?.Invoke(this, (i, g.red, g.green, g.blue));
                }
            }
        }
        Console.WriteLine("Done");
    }
    
    private (byte r, byte g, byte b) ConvertToRGB(byte y, byte u, byte v)
    {
        int r, g, b;
        int tmpy = y - 16;
        int tmpcb = u - 128;
        int tmpcr = v - 128;
        //r = (int)(1.164 * tmpy + 0.0000 * tmpcb + 1.5960 * tmpcr);
        //g = (int)(1.164 * tmpy + -0.392 * tmpcb + -0.813 * tmpcr);
        //b = (int)(1.164 * tmpy + 2.0170 * tmpcb + 0.0000 * tmpcr);
        //r = (int)(1 * tmpy + 0.0000 * tmpcb + 1.139 * tmpcr);
        //g = (int)(1 * tmpy + -0.395 * tmpcb + -0.581 * tmpcr);
        //b = (int)(1 * tmpy + 2.0321 * tmpcb + 0.0000 * tmpcr);
        //r = (int)(1 * tmpy + 0.0000 * tmpcb + 1.402 * tmpcr);
        //g = (int)(1 * tmpy + -0.344 * tmpcb + -0.714 * tmpcr);
        //b = (int)(1 * tmpy + 1.772 * tmpcb + 0.0000 * tmpcr);
        r = (int)(1 * tmpy + 0.0000 * tmpcb + 1.4746 * tmpcr);
        g = (int)(1 * tmpy + -0.1646 * tmpcb + -0.5714 * tmpcr);
        b = (int)(1 * tmpy + 1.8814 * tmpcb + 0.0000 * tmpcr);
        r = Math.Clamp(r, 0, byte.MaxValue);
        g = Math.Clamp(g, 0, byte.MaxValue);
        b = Math.Clamp(b, 0, byte.MaxValue);
        return ((byte)r, (byte)g, (byte)b);
    }

    private (byte[] r, byte[] g, byte[] b) ConvertYCbCrToRGB(byte[] y, byte[] cb, byte[] cr)
    {
        byte[] r = new byte[y.Length];
        byte[] g = new byte[y.Length];
        byte[] b = new byte[y.Length];
        for (int i = 0; i < y.Length; i++)
        {
            (r[i], g[i], b[i]) = ConvertToRGB(y[i], cb[i], cr[i]);
        }
        return (r, g, b);
    }

    private void ConsumeSync(SSTVParameters parameters, ushort[] samples, ref double startIndex)
    {
        for(int i = 0; i < parameters.PixelsPerLine; i++)
        {
            double endIndex = startIndex + BlockSize;
            ResetFilter(ColorFilters);
            for(int j = (int)startIndex; j < endIndex; j++)
            {
                for(int k = 0; k < ColorFilters.Length; k++)
                {
                    ColorFilters[k] = ColorFilters[k].AddSample(samples[j]);
                }
            } 
            double confidence;
            int maxIndex = GetMaxFilter(ColorFilters, out confidence);
            if(maxIndex == 0)
            {   
                i = -1;
                startIndex += parameters.StepSize;
                continue;           
            }
            else
            {
                return;
            }
        }  
    }


    private byte[] ReadLineGoertzel(bool hsyncEnable, SSTVParameters parameters, ushort[] samples, ref double startIndex)
    {
        byte[] bytes = new byte[parameters.PixelsPerLine];
        int blackLevel = 1;
        int whiteLevel = 9;
        int colorStep = byte.MaxValue / 9;
        bool hsync = false;
        double colorSampleCount = SamplesPerMS * (parameters.LineLengthMS + parameters.BlackLevelTimeMS);
        double colorStartIndex = startIndex;
        for(int i = 0; i < parameters.PixelsPerLine; i++)
        {
            double endIndex = startIndex + BlockSize;
            ResetFilter(ColorFilters);
            for(int j = (int)startIndex; j < endIndex; j++)
            {
                for(int k = 0; k < ColorFilters.Length; k++)
                {
                    ColorFilters[k] = ColorFilters[k].AddSample(samples[j]);
                }
            } 
            double confidence;
            int maxIndex = GetMaxFilter(ColorFilters, out confidence);
            if(maxIndex == 0)
            {   
                if(hsyncEnable)
                {
                    i = -1;
                    startIndex += parameters.StepSize;
                    colorStartIndex = startIndex;
                    continue;
                }
            }
            else if(maxIndex <= whiteLevel && maxIndex >= blackLevel)
            {
                int stepShift = (int)(colorStep * confidence);
               
                bytes[i] = (byte)Math.Clamp(((colorStep * (maxIndex - blackLevel)) + stepShift), 0, byte.MaxValue);
            }
            startIndex += parameters.StepSize;
        }  
        startIndex = colorStartIndex + colorSampleCount;
        return bytes;
    }

    public byte ReadVIS(ushort[] samples, ref int startIndex)
    {
        int visStart = startIndex;

        // Read a VIS code.       
        FindVISToneStart(samples, ref startIndex);
        int[] bits = new int[8];
        for(int i = 0; i < 8; i++)
        {
            bits[i] = FindVISTone(samples, ref startIndex);
        }
        FindVISToneStart(samples, ref startIndex);

        int retval = 0;
        retval = retval | bits[6] << 6;
        retval = retval | bits[5] << 5;
        retval = retval | bits[4] << 4;   
        retval = retval | bits[3] << 3;   
        retval = retval | bits[2] << 2;          
        retval = retval | bits[1] << 1;   
        retval = retval | bits[0];
        return (byte)retval;   
    }
}