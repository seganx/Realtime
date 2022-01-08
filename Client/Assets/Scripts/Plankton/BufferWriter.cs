using UnityEngine;

namespace SeganX.Network
{
    public class BufferWriter
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
        public int Length { get; private set; } = 0;

        public BufferWriter(int size)
        {
            buffer = new byte[size];
        }

        public BufferWriter Reset()
        {
            Length = 0;
            return this;
        }

        public BufferWriter Append(System.Array src, int length)
        {
            System.Buffer.BlockCopy(src, 0, buffer, Length, length);
            Length += length;
            return this;
        }

        public BufferWriter AppendChar(char value)
        {
            charArray[0] = value;
            return Append(charArray, sizeof(char));
        }

        public BufferWriter AppendByte(byte value)
        {
            byteArray[0] = value;
            return Append(byteArray, 1);
        }

        public BufferWriter AppendSbyte(sbyte value)
        {
            sbyteArray[0] = value;
            return Append(sbyteArray, 1);
        }

        public BufferWriter AppendBool(bool value)
        {
            byteArray[0] = value ? (byte)1 : (byte)0;
            return Append(byteArray, 1);
        }

        public BufferWriter AppendShort(short value)
        {
            shortArray[0] = value;
            return Append(shortArray, 2);
        }

        public BufferWriter AppendUshort(ushort value)
        {
            ushortArray[0] = value;
            return Append(ushortArray, 2);
        }

        public BufferWriter AppendFloat(float value)
        {
            floatArray[0] = value;
            return Append(floatArray, 4);
        }

        public BufferWriter AppendInt(int value)
        {
            intArray[0] = value;
            return Append(intArray, 4);
        }

        public BufferWriter AppendUint(uint value)
        {
            uintArray[0] = value;
            return Append(uintArray, 4);
        }

        public BufferWriter AppendLong(long value)
        {
            longArray[0] = value;
            return Append(longArray, sizeof(long));
        }

        public BufferWriter AppendBytes(byte[] value, int length)
        {
            return Append(value, length);
        }

        public BufferWriter AppendString(string value)
        {
            var length = (byte)System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, Length + 1);
            buffer[Length] = length;
            Length += length + 1;
            return this;
        }

        public BufferWriter AppendVector2(Vector2 value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            return Append(floatArray, 8);
        }

        public BufferWriter AppendVector3(Vector3 value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            floatArray[2] = value.z;
            return Append(floatArray, 12);
        }

        public BufferWriter AppendVector4(Vector4 value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            floatArray[2] = value.z;
            floatArray[3] = value.w;
            return Append(floatArray, 16);
        }

        public BufferWriter AppendQuaternion(Quaternion value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            floatArray[2] = value.z;
            floatArray[3] = value.w;
            return Append(floatArray, 16);
        }
    }
}