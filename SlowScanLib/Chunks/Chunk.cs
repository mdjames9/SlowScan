namespace SlowScan.Chunks;
    
public class Chunk
{

    #region RecognizedChunks

    public const uint FormatChunk = 544501094;

    public const uint DataChunk = 1635017060;

    #endregion RecognizedChunks

    public string ChunkId {get; private set;}

    public uint ChunkIdNumber {get; private set;}
    public uint ChunkSize {get; private set;}

    public Chunk(FileStream fs)
    {
        byte[] id = new byte[4];
        byte[] length = new byte[4];
        fs.ReadExactly(id, 0, 4);
        fs.ReadExactly(length, 0, 4);
        ChunkId = System.Text.Encoding.ASCII.GetString(id);
        ChunkIdNumber = BitConverter.ToUInt32(id);
        ChunkSize = BitConverter.ToUInt32(length);
    }

    public Chunk(string name, uint id, uint size)
    {
        ChunkId = name;
        ChunkIdNumber = id;
        ChunkSize = size;
    }



    public static Chunk GetNextChunk(FileStream fs)
    {
        byte[] id = new byte[4];
        byte[] length = new byte[4];
        fs.ReadExactly(id, 0, 4);
        fs.ReadExactly(length, 0, 4);
        var name = System.Text.Encoding.ASCII.GetString(id);
        var number = BitConverter.ToUInt32(id);
        var size = BitConverter.ToUInt32(length);
        switch(number)
        {
            case FormatChunk:
                return new FormatChunk(fs, name, number, size);
            case DataChunk:
                return new DataChunk(fs, name, number, size);
            default:
                // Skip this chunk we don't recognize it.
                fs.Seek(size, SeekOrigin.Current);
                break;
        }
        return null;
    }

}