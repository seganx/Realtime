using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeganX.Realtime
{
    public class NetView
    {
        private NetPlayer owner = null;

        public ushort Id { get; private set; } = 0;
        public event Action<byte, byte[], byte> OnReceived = null;

        public NetPlayer Owner => owner;
        public bool IsMine => owner == null || owner.IsMine;


        public NetView Send(byte eventCode, bool reliable, BufferWriter data, Target target = Target.Other, sbyte otherId = -1)
        {
            writer.Reset();
            writer.AppendUshort(Id);
            writer.AppendByte(eventCode);
            writer.AppendBytes(data.Bytes, data.Length);
            Radio.Send(EventCode.EventsView, reliable, writer, target, otherId);
            return this;
        }

        private NetView(ushort viewId, NetPlayer owner)
        {
            Id = viewId;
            SetOwner(owner);
        }

        private void SetOwner(NetPlayer owner)
        {
            this.owner = owner;
        }

        public override string ToString()
        {
            return $"Id:{Id} Owner:[{Owner}]";
        }


        //////////////////////////////////////////////////////
        /// STATIC MEMBERS
        //////////////////////////////////////////////////////
        private static readonly List<NetView> all = new List<NetView>();
        private static readonly BufferReader reader = new BufferReader(null);
        private static readonly BufferWriter writer = new BufferWriter(256);

        public static event Action<NetView, byte[], int> OnViewCreated = null;
        public static event Action<NetView> OnViewRemoved = null;


        public static void CreateView(BufferWriter data)
        {
            ushort newId = (ushort)((Radio.PlayerId + 1) * 256);
            while (all.Exists(x => x.Id == newId))
                newId++;

            var buffer = new BufferWriter(256);
            buffer.AppendUshort(newId);
            buffer.AppendBytes(data.Bytes, data.Length);
            Radio.Send(EventCode.CreateView, true, buffer);

            ViewCreated(Radio.Player, newId, data.Bytes, data.Length);
        }

        public static void RemoveView(ushort id)
        {
            var buffer = new BufferWriter(256);
            buffer.AppendUshort(id);
            Radio.Send(EventCode.RemoveView, true, buffer);

            ViewRemoved(id);
        }

        static NetView()
        {
            var cacheBytes = new byte[256];

            Radio.OnPlayerRemoved += netPlayer =>
            {
                if (netPlayer.IsMine == false) return;
                Clear();
            };

            Radio.OnMasterChanged += () =>
            {
                if (Radio.IsMaster)
                {
                    foreach (var view in all)
                    {
                        if (view.Id > 250)
                            view.SetOwner(Radio.Player);
                    }
                }
            };

            Radio.OnReceived += (netPlayer, eventCode, data, dataSize) =>
            {
                reader.Reset(data);

                switch (eventCode)
                {
                    case EventCode.CreateView:
                        {
                            var viewId = reader.ReadUshort();
                            dataSize -= 2;
                            reader.ReadBytes(cacheBytes, dataSize);
                            ViewCreated(netPlayer, viewId, cacheBytes, dataSize);
                        }
                        break;
                    case EventCode.RemoveView:
                        {
                            var viewId = reader.ReadUshort();
                            ViewRemoved(viewId);
                        }
                        break;
                    case EventCode.EventsView:
                        {
                            var viewId = reader.ReadUshort();
                            var view = all.Find(x => x.Id == viewId);
                            if (view != null)
                            {
                                var viewEvent = reader.ReadByte();
                                dataSize -= 3;
                                reader.ReadBytes(cacheBytes, dataSize);
                                view.OnReceived?.Invoke(viewEvent, cacheBytes, dataSize);
                            }
                        }
                        break;
                }
            };
        }

        private static void ViewCreated(NetPlayer owner, ushort id, byte[] data, int size)
        {
            var view = all.Find(x => x.Id == id);
            if (view != null) return;
            view = new NetView(id, owner);
            all.Add(view);
            OnViewCreated?.Invoke(view, data, size);
        }

        private static void ViewRemoved(ushort id)
        {
            var view = all.Find(x => x.Id == id);
            if (view == null) return;
            all.Remove(view);
            OnViewRemoved?.Invoke(view);
        }

        public static void Clear()
        {
            foreach (var view in all)
                OnViewRemoved?.Invoke(view);
            all.Clear();
        }
    }
}