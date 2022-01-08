using System;
using UnityEngine;
using SeganX.Network.Internal;

namespace SeganX.Network
{
    public class NetPlayer
    {
        private float lastActiveTime = 0;

        public sbyte Id { get; private set; } = -1;
        public bool IsActive { get; private set; } = true;

        public bool IsMine => Radio.PlayerId == Id;
        public bool IsOther => Radio.PlayerId != Id;

        public event Action<BufferReader, byte> OnReceived = (buffer, size) => { };
        public event Action OnDestory = () => { };

        public NetPlayer(sbyte id)
        {
            Id = id;
        }

        public float Update(float activeTimeout)
        {
            var deltaTime = Time.time - lastActiveTime;
            IsActive = deltaTime < activeTimeout;
            return deltaTime;
        }

        public void CallReceived(BufferReader buffer, byte dataSize)
        {
            lastActiveTime = Time.time;
            OnReceived(buffer, dataSize);
        }

        public void CallDestroy()
        {
            OnDestory();
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