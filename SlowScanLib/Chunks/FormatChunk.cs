namespace SlowScan.Chunks;

public class FormatChunk : Chunk
{

    public ushort FormatCode {get; set;}

    public ushort ChannelCount {get; set;}

    public uint SampleRate {get;set;}

    public uint DataRate{get;set;}

    public ushort BlockSize {get; set;}

    public ushort BitsPerSample {get; set;}

    public ushort ExtensionSize {get; set;}

    public ushort ValidBits {get; set;}

    public uint ChannelMask {get; set;}

    public Guid SubFormat {get; set;}
    public FormatChunk(FileStream fs, string name, uint id, uint size) : base(name, id, size)
    {
        byte[] bytes = new byte[4];

        fs.ReadExactly(bytes, 0, 2);
        FormatCode = BitConverter.ToUInt16(bytes);
        fs.ReadExactly(bytes, 0, 2);
        ChannelCount = BitConverter.ToUInt16(bytes);
        fs.ReadExactly(bytes, 0, 4);
        SampleRate = BitConverter.ToUInt32(bytes);
        fs.ReadExactly(bytes, 0, 4);
        DataRate = BitConverter.ToUInt32(bytes);
        fs.ReadExactly(bytes, 0, 2);
        BlockSize = BitConverter.ToUInt16(bytes);
        fs.ReadExactly(bytes, 0, 2);
        BitsPerSample = BitConverter.ToUInt16(bytes);

        if(ChunkSize == 18 || ChunkSize == 40)
        {
            fs.ReadExactly(bytes, 0, 2);
            ExtensionSize = BitConverter.ToUInt16(bytes);
        }
        if(ExtensionSize == 22)
        {
            fs.ReadExactly(bytes, 0, 2);
            ValidBits = BitConverter.ToUInt16(bytes);
            fs.ReadExactly(bytes, 0, 4);
            ChannelMask = BitConverter.ToUInt32(bytes);
            byte[] guidBytes = new byte[16];
            fs.ReadExactly(guidBytes, 0, 16);
            SubFormat = new Guid(bytes);
        }
    }
}