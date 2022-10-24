using System;
using UnityEngine;

namespace SeganX.Realtime
{
    public class NetPlayer
    {
        private float lastActiveTime = 0;

        public sbyte Id { get; private set; } = -1;
        public string Name { get; private set; } = string.Empty;
        public bool IsMine { get; private set; } = true;
        public bool IsOther { get; private set; } = false;
        public bool IsActive { get; private set; } = true;

        internal void SetId(sbyte id)
        {
            Id = id;
            IsMine = Radio.PlayerId == id;
            IsOther = Radio.PlayerId != id;
        }

        internal void SetName(string playerName)
        {
            Name = playerName;
        }

        internal void Stamptime()
        {
            lastActiveTime = Time.time;
        }

        internal float Update(float activeTimeout)
        {
            var deltaTime = Time.time - lastActiveTime;
            IsActive = deltaTime < activeTimeout;
            return deltaTime;
        }

        public override string ToString()
        {
            return $"Id:{Id} IsActive:{IsActive} IsMine:{IsMine}";
        }
    }
}