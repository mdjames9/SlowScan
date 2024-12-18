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
        BlockSize = 480;

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
            for(int i = startIndex; i < endIndex; i++)
            {
                foreach(FilterType index in Enum.GetValues<FilterType>())
                {
                    ToneFilters[(int)index] = ToneFilters[(int)index].AddSample(samples[i]);
                }
            }
            FilterType currentMax = (FilterType)GetMaxFilter(ToneFilters);
            cv.AddBlock(currentMax, startIndex);
            startIndex += BlockSize;

        }

        startIndex = optimalStartIndex + msLength * SamplesPerMS;
        return true;

    }

    private int GetMaxFilter(Goertzel[] filter)
    {
        int maxType = -1;
        double maxMagnitude = 0;
        for(int i = 0; i < filter.Length; i++)
        {
            if(filter[i].NormResponse > maxMagnitude)
            {
                maxType = i;
                maxMagnitude = filter[i].NormResponse;
            }
            filter[i] = filter[i].Reset();
        }

        return maxType;
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
            if(prevTone.NormResponse > start.NormResponse)
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
        if (zero.NormResponse > one.NormResponse)
        {
            return 0;
        }
        else
        {
            return 1;
        }
    }
    public (byte[], byte[], byte[]) ReadLine(SSTVParameters sstv, ushort[] samples, ref int startIndex)
    {
        // HSYNC is handled by the line method. It will cause color processing to restart.
        // Scottie would need to consume the intial pulse first.
        byte[] green = ReadLineGoertzel(sstv, samples, ref startIndex);
        byte[] blue = ReadLineGoertzel(sstv, samples, ref startIndex);
        byte[] red = ReadLineGoertzel(sstv, samples, ref startIndex);
        return (red, green, blue);
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
            SSTVParameters stv = new(0, 0, 0, 0);
            if (viscode == 96)
            {
                stv = new SSTVParameters(0, 0, 640, 496);
                Console.WriteLine("VIS indicates PD 180");
            }
            else if (viscode == 60)
            {
                // This is Scottie S1.
                stv = new SSTVParameters(0, 0, 256, 256);
                Console.WriteLine("VIS indicates Scottie.");
            }
            else if (viscode == 44 || viscode == 32)
            {
                // Martin M1!
                stv = new SSTVParameters(146.432, 4.862, 256, 256);
                Console.WriteLine("VIS Indicates Martin M1.");
            }
            else if (viscode == 40)
            {
                stv = new SSTVParameters(73.216, 4.862, 160, 256);
                Console.WriteLine("VIS Indicates Martin M2.");
            }
            SignalFound?.Invoke(this, (stv.PixelsPerLine, stv.LinesPerFrame));

            int imageStart = index;
            for(int i = 0; i < stv.LinesPerFrame; i++)
            {
                int lineStart = index;
                (byte[], byte[], byte[]) g = ReadLine(stv, mySamples, ref index);
                LineAvailable?.Invoke(this, (i, g.Item1, g.Item2, g.Item3));
            }
        }
        Console.WriteLine("Done");
    }
    
    private byte[] ReadLineGoertzel(SSTVParameters parameters, ushort[] samples, ref int startIndex)
    {
        byte[] bytes = new byte[parameters.PixelsPerLine];
        int blackLevel = 1;
        int whiteLevel = 9;
        int colorStep = byte.MaxValue / 9;
        bool hsync = false;
        do
        {
            hsync = false;
            try
            {
                for(int i = 0; i < parameters.PixelsPerLine; i++)
                {
                    int endIndex = startIndex + BlockSize;
                    for(int j = startIndex; j < endIndex; j++)
                    {
                        for(int k = 0; k < ColorFilters.Length; k++)
                        {
                            ColorFilters[k] = ColorFilters[k].AddSample(samples[j]);
                        }
                    } 
                    int maxIndex = GetMaxFilter(ColorFilters);
                    if(maxIndex == 0)
                    {   
                        throw new SyncException();
                    }
                    else if(maxIndex <= whiteLevel && maxIndex >= blackLevel)
                    {
                        bytes[i] = (byte)(colorStep * (maxIndex - blackLevel));
                    }
                    startIndex += parameters.StepSize;
                }
            }
            catch(SyncException)
            {
                hsync = true;
                startIndex += parameters.StepSize;
            }
        }
        while(hsync);
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