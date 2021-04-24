public class Buffer
{
    private readonly byte[] buffer = null;

    public byte[] Bytes => buffer;
    public int Length { get; private set; } = 0;

    public Buffer(int size)
    {
        buffer = new byte[size];
    }

    public void AppendByte(byte value)
    {
        byte[] v = { value };
        System.Buffer.BlockCopy(v, 0, buffer, Length, 1);
        Length += 1;
    }

    public void AppendUshort(ushort value)
    {
        ushort[] v = { value };
        System.Buffer.BlockCopy(v, 0, buffer, Length, 2);
        Length += 2;
    }

    public void AppendUint(uint value)
    {
        uint[] v = { value };
        System.Buffer.BlockCopy(v, 0, buffer, Length, 4);
        Length += 4;
    }

    public void AppendBytes(byte[] value)
    {
        System.Buffer.BlockCopy(value, 0, buffer, Length, value.Length);
        Length += value.Length;
    }
}
