namespace SlowScan.Chunks;

public class RIFFChunk : Chunk
{
    public string WAVEId {get; set;}

    public uint WaveChunksSize {get; set;}

    public RIFFChunk(FileStream fs) : base(fs)
    {
        byte[] id = new byte[4];
        byte[] length = new byte[4];
        fs.ReadExactly(id, 0, 4);
        WAVEId = System.Text.Encoding.ASCII.GetString(id);
        WaveChunksSize = ChunkSize - 4;
    }
}