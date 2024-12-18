using MathNet.Numerics;
using SlowScan.Chunks;
namespace SlowScan;

public class WavReader
{
    FileStream fileHandle;
    public FormatChunk FormatChunk {get; set;}

    public DataChunk DataChunk {get; set;}
    public void Open(string fileName)
    {
        fileHandle = new FileStream(fileName, FileMode.Open);
        // The first chunk is a riff chunk..
        // After that it depends.
        RIFFChunk riffChunk = new RIFFChunk(fileHandle);
        Chunk format;
        do
        {
            format = Chunk.GetNextChunk(fileHandle);
        } while(!(format is FormatChunk));
        FormatChunk = (FormatChunk)format;
        Chunk data;
        do
        {
            data = Chunk.GetNextChunk(fileHandle);
        } while(!(data is DataChunk));
        this.DataChunk = (DataChunk)data;
    }

    public IEnumerable<ushort> GetSamples()
    {
        return this.DataChunk.ReadAsShort(fileHandle);
    }

    public uint GetSampleRate()
    {
        return this.FormatChunk.SampleRate;
    }
}
