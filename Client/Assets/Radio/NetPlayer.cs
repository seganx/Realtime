using System;
using UnityEngine;

namespace SeganX.Network
{
    public class NetPlayer
    {
        private float lastActiveTime = 0;

        public sbyte Id { get; private set; } = -1;
        public bool IsMine { get; private set; } = true;
        public bool IsOther { get; private set; } = false;
        public bool IsActive { get; private set; } = true;

        public event Action<byte[], byte> OnReceived = (bytes, size) => { };

        private readonly byte[] bytes = new byte[512];

        public void SetId(sbyte id)
        {
            Id = id;
            IsMine = Radio.PlayerId == id;
            IsOther = Radio.PlayerId != id;
        }

        public float Update(float activeTimeout)
        {
            var deltaTime = Time.time - lastActiveTime;
            IsActive = deltaTime < activeTimeout;
            return deltaTime;
        }

        internal void CallReceived(BufferReader buffer, byte dataSize)
        {
            lastActiveTime = Time.time;
            buffer.ReadBytes(bytes, dataSize);
            OnReceived(bytes, dataSize);
        }

        public void SendUnreliable(Target target, BufferWriter data, sbyte otherId = 0)
        {
            if (IsOther) return;
            Radio.SendUnreliable(target, data, otherId);
        }

        public void SendReliable(Target target, BufferWriter data, sbyte otherId = 0)
        {
            if (IsOther) return;
            Radio.SendReliable(target, data, otherId);
        }

        public override string ToString()
        {
            return $"Id:{Id} IsActive:{IsActive} IsMine:{IsMine}";
        }
    }
}