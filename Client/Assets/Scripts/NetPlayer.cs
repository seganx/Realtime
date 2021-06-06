using System;
using SeganX.Plankton.Net;

namespace SeganX.Plankton
{
    public class NetPlayer
    {
        private readonly Netlayer netlayer;

        public int Id { get; private set; } = 0;
        public float LastActiveTime { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public bool IsMine => netlayer.PlayerId == Id;
        public bool IsOther => netlayer.PlayerId != Id;

        public Action<BufferReader> OnReceived = Buffer => { };
        public Action OnDestory = () => { };

        public NetPlayer(Netlayer netlayer, int id)
        {
            this.netlayer = netlayer;
            Id = id;
        }

        public void Send(SendType type, BufferWriter data, byte otherId = 0)
        {
            if (IsOther) return;
            netlayer.Send(type, data, otherId);
        }
    }
}