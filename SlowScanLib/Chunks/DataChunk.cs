using System.IO;
using System.Runtime.CompilerServices;
namespace SlowScan.Chunks;
public class DataChunk : Chunk
{
    public long DataStartOffset {get; set;}
    public uint DataLength { get { return ChunkSize; } }
    public byte[] SampleData {get; set;}
    public DataChunk(FileStream fs, string name, uint number, uint size) : base(name, number, size)
    {
        DataStartOffset = fs.Position;
        fs.Seek(ChunkSize, SeekOrigin.Current);
    }

    public IEnumerable<ushort> ReadAsShort(FileStream fs)
    {
        fs.Seek(DataStartOffset, SeekOrigin.Begin);
        var remaining = ChunkSize;
        while (remaining > 0)
        {
            byte[] bytes = new byte[2];
            if(fs.CanRead)
            {
                fs.ReadExactly(bytes, 0, 2);
                remaining -= 2;
                yield return BitConverter.ToUInt16(bytes);
            }
            else
            {
                break;
            }

        }
    }
}