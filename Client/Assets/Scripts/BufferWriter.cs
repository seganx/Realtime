using UnityEngine;

namespace SeganX.Plankton
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

        public void Reset()
        {
            Length = 0;
        }

        public void Append(System.Array src, int length)
        {
            System.Buffer.BlockCopy(src, 0, buffer, Length, length);
            Length += length;
        }

        public void AppendChar(char value)
        {
            charArray[0] = value;
            Append(charArray, sizeof(char));
        }

        public void AppendByte(byte value)
        {
            byteArray[0] = value;
            Append(byteArray, 1);
        }

        public void AppendSbyte(sbyte value)
        {
            sbyteArray[0] = value;
            Append(sbyteArray, 1);
        }

        public void AppendBool(bool value)
        {
            byteArray[0] = value ? (byte)1 : (byte)0;
            Append(byteArray, 1);
        }

        public void AppendShort(short value)
        {
            shortArray[0] = value;
            Append(shortArray, 2);
        }

        public void AppendUshort(ushort value)
        {
            ushortArray[0] = value;
            Append(ushortArray, 2);
        }

        public void AppendFloat(float value)
        {
            floatArray[0] = value;
            Append(floatArray, 4);
        }

        public void AppendInt(int value)
        {
            intArray[0] = value;
            Append(intArray, 4);
        }

        public void AppendUint(uint value)
        {
            uintArray[0] = value;
            Append(uintArray, 4);
        }

        public void AppendLong(long value)
        {
            longArray[0] = value;
            Append(longArray, sizeof(long));
        }

        public void AppendBytes(byte[] value, int length)
        {
            Append(value, length);
        }

        public void AppendString(string value)
        {
            var length = (byte)System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, Length + 1);
            buffer[Length] = length;
            Length += length + 1;
        }

        public void AppendVector2(Vector2 value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            Append(floatArray, 8);
        }

        public void AppendVector3(Vector3 value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            floatArray[2] = value.z;
            Append(floatArray, 12);
        }

        public void AppendVector4(Vector4 value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            floatArray[2] = value.z;
            floatArray[3] = value.w;
            Append(floatArray, 16);
        }

        public void AppendQuaternion(Quaternion value)
        {
            floatArray[0] = value.x;
            floatArray[1] = value.y;
            floatArray[2] = value.z;
            floatArray[3] = value.w;
            Append(floatArray, 16);
        }
    }
}