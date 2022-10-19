using System.Collections.Generic;

namespace SeganX.Network
{
    public enum MessageType : byte
    {
        Ping = 1,
        Login = 10,
        Logout = 11,
        CreateRoom = 20,
        Join = 30,
        Leave = 31,
        Unreliable = 40,
        Reliable = 41,
        Relied = 42
}

    public enum Target : byte
    {
        All = 1,
        Other = 2,
        Player = 3
    }

    public enum Error : sbyte
    {
        NoError = 0,
        Invalid = -1,
        Expired = -2,
        IsFull = -3,
        JoinFailed = -4,
    }

    [System.Flags]
    public enum Flag : byte
    {
        Nothing = 0,
        Master = 1
    }

    public struct MatchmakingParams
    {
        public int a, b, c, d;

        public override string ToString()
        {
            return $"{a}|{b}|{c}|{d}";
        }
    }

    public struct MatchmakingRanges
    {
        public int aMin, aMax;
        public int bMin, bMax;
        public int cMin, cMax;
        public int dMin, dMax;

        public override string ToString()
        {
            return $"a[{aMin},{aMax}] b[{bMin},{bMax}] c[{cMin},{cMax}] d[{dMin},{dMax}]";
        }
    }

    namespace Internal
    {
        public class ClientInfo
        {
            public byte[] device = null;
            public uint token = 0;
            public short id = -1;
            public short room = -1;
            public sbyte index = -1;
        }

        public class Message
        {
            public float delayTime = 0;
            public float maxDelayTime = 0.5f;
        }

        public class RequestMessage : Message
        {
            public MessageType type = 0;
            public int dataSize = 0;
            public readonly byte[] data = new byte[512];
            public System.Action<Error, BufferReader> callback = null;
        }

        public class ReliableMessage : Message
        {
            public byte ack = 0;
            public sbyte targetIndex = 0;
            public BufferWriter buffer = new BufferWriter(512);
            public int retryCount = 0;
        }

        public class Pool<T> : List<T> where T : new()
        {
            private int index = 0;

            public Pool(int capacity) : base(capacity)
            {
                for (int i = 0; i < capacity; i++)
                    Add(new T());
            }

            public T Peek()
            {
                return this[index++ % Count];
            }
        }

        //////////////////////////////////////////////////////
        /// STATIC MEMBERS
        //////////////////////////////////////////////////////
        public static partial class Extension
        {
            public static bool AddUnique<T>(this List<T> self, T item, System.Predicate<T> match)
            {
                if (self.Exists(match) == false)
                {
                    self.Add(item);
                    return true;
                }
                else return false;
            }
        }
    }
}