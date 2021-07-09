using UnityEngine;

namespace SeganX.Plankton
{
    public class BufferReader
    {
        private readonly byte[] buffer = null;
        private readonly char[] charArray = new char[1];
        private readonly byte[] byteArray = new byte[1];
        private readonly sbyte[] sbyteArray = new sbyte[1];
        private readonly short[] shortArray = new short[1];
        private readonly ushort[] ushortArray = new ushort[1];
        private readonly float[] floatArray = new float[4];
        private readonly int[] intArray = new int[1];
        private readonly uint[] uintArray = new uint[1];
        private readonly long[] longArray = new long[1];

        public byte[] Bytes => buffer;
        public int Pos { get; private set; } = 0;

        public BufferReader(int size)
        {
            buffer = new byte[size];
        }

        public BufferReader(byte[] bytes)
        {
            buffer = bytes;
        }

        public void Reset()
        {
            Pos = 0;
        }

        public void Read(System.Array dest, int length)
        {
            System.Buffer.BlockCopy(buffer, Pos, dest, 0, length);
            Pos += length;
        }

        public char ReadChar()
        {
            Read(charArray, sizeof(char));
            return charArray[0];
        }

        public byte ReadByte()
        {
            Read(byteArray, 1);
            return byteArray[0];
        }

        public sbyte ReadSbyte()
        {
            Read(sbyteArray, 1);
            return sbyteArray[0];
        }

        public bool ReadBool()
        {
            Read(byteArray, 1);
            return byteArray[0] > 0;
        }

        public short ReadShort()
        {
            Read(shortArray, 2);
            return shortArray[0];
        }

        public ushort ReadUshort()
        {
            Read(ushortArray, 2);
            return ushortArray[0];
        }

        public float ReadFloat()
        {
            Read(floatArray, 4);
            return floatArray[0];
        }

        public int ReadInt()
        {
            Read(intArray, 4);
            return intArray[0];
        }

        public uint ReadUint()
        {
            Read(uintArray, 4);
            return uintArray[0];
        }

        public long ReadLong()
        {
            Read(longArray, sizeof(long));
            return longArray[0];
        }

        public void ReadBytes(byte[] value, int length)
        {
            Read(value, length);
        }

        public string ReadString()
        {
            var length = ReadByte();
            var res = System.Text.Encoding.UTF8.GetString(buffer, Pos, length);
            Pos += length;
            return res;
        }

        public Vector2 ReadVector2()
        {
            Read(floatArray, 8);
            Vector2 value;
            value.x = floatArray[0];
            value.y = floatArray[1];
            return value;
        }

        public Vector3 ReadVector3()
        {
            Read(floatArray, 12);
            Vector3 value;
            value.x = floatArray[0];
            value.y = floatArray[1];
            value.z = floatArray[2];
            return value;
        }

        public Vector4 ReadVector4()
        {
            Read(floatArray, 16);
            Vector4 value;
            value.x = floatArray[0];
            value.y = floatArray[1];
            value.z = floatArray[2];
            value.w = floatArray[3];
            return value;
        }

        public Quaternion ReadQuaternion()
        {
            Read(floatArray, 16);
            Quaternion value;
            value.x = floatArray[0];
            value.y = floatArray[1];
            value.z = floatArray[2];
            value.w = floatArray[3];
            return value;
        }
    }
}