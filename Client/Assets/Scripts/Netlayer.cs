using UnityEngine;
using System.Net;
using System.Threading.Tasks;

namespace SeganX.Plankton
{
    public enum SendType : byte
    {
        All = 1,
        Other = 2,
        Player = 3
    }

    namespace Net
    {
        public class Netlayer
        {
            private const byte packetTypePing = 1;
            private const byte packetTypeLogin = 2;
            private const byte packetTypeLogout = 3;
            private const byte packetTypeMessage = 4;
            private const byte packetTypeExpired = 5;

            private uint token  = 0;
            private float aliveTime = -10;
            private byte[] device = null;
            private readonly BufferReader receivedBuffer = new BufferReader(256);
            private readonly BufferWriter loginBuffer = new BufferWriter(64);
            private readonly BufferWriter sendBuffer = new BufferWriter(256);
            private readonly BufferWriter pingBuffer = new BufferWriter(16);
            private readonly NetSocket socket = new NetSocket();

            private long Ticks => System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond;
            private float DeathTime => Time.unscaledTime - aliveTime;

            public IPEndPoint ServerAddress = new IPEndPoint(0, 0);
            public long Ping { get; private set; } = 0;
            public byte PlayerId { get; private set; } = 0;
            public ushort RoomId { get; private set; } = 0;
            public bool Connected => DeathTime < 5.0f;

            public void Start(byte[] devicebytes)
            {
                if (device != null)
                {
                    Debug.LogWarning("[Network] Already started!");
                    return;
                }

                if (devicebytes.Length != 32)
                {
                    Debug.LogError("[Network] Start: Device id must be 32 bytes");
                    return;
                }

                socket.Open(32000, 35000);
                device = devicebytes;
                aliveTime = -10;
                Login();
                Pinger();
            }

            public void Stop()
            {
                if (device == null) return;
                sendBuffer.Reset();
                sendBuffer.AppendByte(packetTypeLogout);
                sendBuffer.AppendUshort(RoomId);
                sendBuffer.AppendByte(PlayerId);
                sendBuffer.AppendUint(token);
                sendBuffer.AppendUint(ComputeChecksum(sendBuffer.Bytes, sendBuffer.Length));
                socket.Send(ServerAddress, sendBuffer.Bytes, sendBuffer.Length);

                Ping = 0;
                token = 0;
                device = null;
                aliveTime = -10;
                socket.Close();
            }

            private async void Login()
            {
                while (device != null)
                {
                    if (token == 0)
                    {
                        loginBuffer.Reset();
                        loginBuffer.AppendByte(packetTypeLogin);
                        loginBuffer.AppendBytes(device, 32);
                        loginBuffer.AppendUint(ComputeChecksum(loginBuffer.Bytes, loginBuffer.Length));
                        socket.Send(ServerAddress, loginBuffer.Bytes, loginBuffer.Length);
                    }
                    await Task.Delay(1000);
                }
            }

            private async void Pinger()
            {
                while (device != null)
                {
                    pingBuffer.Reset();
                    pingBuffer.AppendByte(packetTypePing);
                    pingBuffer.AppendLong(Ticks);
                    socket.Send(ServerAddress, pingBuffer.Bytes, pingBuffer.Length);
                    await Task.Delay(700);
                }
            }

            public void Send(SendType type, BufferWriter data, byte otherId = 0)
            {
                if (device == null || token == 0) return;
                if (data.Length > 230)
                {
                    Debug.LogError("[Network] Data length must be lees that 230 byes");
                    return;
                }

                byte option = 0;
                switch (type)
                {
                    case SendType.All: option = 1; break;
                    case SendType.Other: option = 2; break;
                    case SendType.Player: option = 3; break;
                }

                sendBuffer.Reset();
                sendBuffer.AppendByte(packetTypeMessage);
                sendBuffer.AppendUshort(RoomId);
                sendBuffer.AppendByte(PlayerId);
                sendBuffer.AppendUint(token);
                sendBuffer.AppendByte(otherId);
                sendBuffer.AppendByte(option);
                sendBuffer.AppendByte((byte)data.Length);
                sendBuffer.AppendBytes(data.Bytes, data.Length);
                socket.Send(ServerAddress, sendBuffer.Bytes, sendBuffer.Length);

            }

            public int Receive(ref int sender, byte[] destBuffer)
            {
                if (device == null) return 0;

                var packsize = socket.Receive(receivedBuffer.Bytes);
                if (packsize < 1) return 0;

                receivedBuffer.Reset();
                switch (receivedBuffer.ReadByte())
                {
                    case packetTypePing:
                        {
                            Ping = (Ticks - receivedBuffer.ReadLong());
                            aliveTime = Time.unscaledTime;
                        }
                        break;

                    case packetTypeLogin:
                        {
                            var roomId = receivedBuffer.ReadUshort();
                            var playerId = receivedBuffer.ReadByte();
                            var rectoken = receivedBuffer.ReadUint();
                            var checksum = receivedBuffer.ReadUint();
                            if (checksum == ComputeChecksum(receivedBuffer.Bytes, 8))
                            {
                                token = rectoken;
                                RoomId = roomId;
                                sender = PlayerId = playerId;
                            }
                            return 0;
                        }

                    case packetTypeMessage:
                        {
                            sender = receivedBuffer.ReadByte();
                            receivedBuffer.ReadBytes(destBuffer, packsize - 2);
                            return packsize - 2;
                        }

                    case packetTypeExpired:
                        {
                            token = 0;
                        }
                        break;
                }
                return 0;
            }

            private uint ComputeChecksum(byte[] buffer, int length)
            {
                uint checksum = 0;
                for (int i = 0; i < length; i++)
                    checksum += 64548 + (uint)buffer[i] * 6597;
                return checksum;
            }
        }
    }
}