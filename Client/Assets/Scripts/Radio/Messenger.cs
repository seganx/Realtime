using UnityEngine;
using System.Net;

namespace SeganX.Network.Internal
{
    public class Messenger
    {
        private const string logName = "[Network] [Messenger]";
        private const float delayFactor = 1;

        private ClientInfo clientInfo = new ClientInfo();
        private readonly Transmitter transmitter = new Transmitter();
        private readonly BufferWriter sendBuffer = new BufferWriter(256);

        public bool Started => clientInfo.device != null;
        public uint Token => clientInfo.token;
        public short Id => clientInfo.id;
        public short Room => clientInfo.room;
        public sbyte Index => clientInfo.index;
        public bool Loggedin => clientInfo.token != 0;

        public Flag Flag { get; private set; } = 0;
        public long ServerTime { get; private set; } = 0;

        public void Start(byte[] devicebytes, IPEndPoint serverAddress, System.Action<Error, sbyte, BufferReader, byte> OnReceivedMessage)
        {
            if (Started)
            {
                Debug.LogWarning("[Network] Already started!");
                return;
            }

            if (devicebytes.Length != 32)
            {
                Debug.LogError("[Network] Start: Device id must be 32 bytes");
                return;
            }

            clientInfo.device = devicebytes;
            transmitter.Start(clientInfo, serverAddress, OnReceivedMessage);
        }

        public void Stop()
        {
            if (Started == false) return;

            Logout();
            transmitter.Stop();
            clientInfo = new ClientInfo();
        }

        public void Update(float elapsedTime)
        {
            transmitter.Update(elapsedTime);
        }

        public void Login(System.Action<Error> callback)
        {
            if (Loggedin)
            {
                callback(Error.NoError);
                return;
            }

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Login)
                .AppendBytes(clientInfo.device, 32)
                .AppendUint(ComputeChecksum(sendBuffer.Bytes, sendBuffer.Length));

            Debug.Log($"{logName} Login {clientInfo.device}");
            transmitter.SendRequestToServer(MessageType.Login, sendBuffer.Bytes, sendBuffer.Length, delayFactor, (error, buffer) =>
            {
                if (error == Error.NoError)
                {
                    var rectoken = buffer.ReadUint();
                    var reclobby = buffer.ReadShort();
                    var recroom = buffer.ReadShort();
                    var recindex = buffer.ReadSbyte();
                    var checksum = buffer.ReadUint();
                    if (checksum == ComputeChecksum(buffer.Bytes, 11))
                    {
                        clientInfo.token = rectoken;
                        clientInfo.id = reclobby;
                        clientInfo.room = recroom;
                        clientInfo.index = recindex;
                        callback?.Invoke(error);
                    }
                    else callback?.Invoke(Error.Invalid);
                }
                else callback?.Invoke(error);

                Debug.Log($"{logName} Login response Token:{clientInfo.token} Id:{clientInfo.id} Room:{clientInfo.room} Index:{clientInfo.index}");
            });
        }

        public void Logout()
        {
            if (clientInfo.token == 0 || clientInfo.device == null) return;

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Logout)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(clientInfo.room)
                .AppendSbyte(clientInfo.index)
                .AppendUint(ComputeChecksum(sendBuffer.Bytes, sendBuffer.Length));

            transmitter.SendRequestToServer(MessageType.Logout, sendBuffer.Bytes, sendBuffer.Length, delayFactor, null);
        }

        public void SendPing(System.Action<Error, long> callback)
        {
            if (clientInfo.token == 0 || clientInfo.device == null) return;

            long Tick() => System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond;

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Ping)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(clientInfo.room)
                .AppendSbyte(clientInfo.index)
                .AppendLong(Tick());

            transmitter.SendRequestToServer(MessageType.Ping, sendBuffer.Bytes, sendBuffer.Length, delayFactor, (error, buffer) =>
            {
                long sentTick = buffer.ReadLong();
                ServerTime = buffer.ReadLong();
                Flag = (Flag)buffer.ReadByte();
                callback?.Invoke(error, Tick() - sentTick);
            });
        }

        public void GetRooms(short startRoomIndex, byte count, bool excludeFullRooms, System.Action<Error, byte, byte[]> callback)
        {
            if (clientInfo.device == null || clientInfo.token == 0) return;

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Rooms)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendByte(excludeFullRooms ? (byte)1 : (byte)0)
                .AppendShort(startRoomIndex)
                .AppendByte(count);

            transmitter.SendRequestToServer(MessageType.Rooms, sendBuffer.Bytes, sendBuffer.Length, delayFactor, (error, buffer) =>
            {
                var roomCount = buffer.ReadByte();
                var rooms = new byte[roomCount];
                buffer.ReadBytes(rooms, roomCount);
                callback?.Invoke(error, roomCount, rooms);
            });
        }

        public void JoinRoom(short roomIndex = -1, System.Action<Error, short, sbyte> callback = null)
        {
            if (clientInfo.device == null || clientInfo.token == 0) return;

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Join)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(roomIndex);

            Debug.Log($"{logName} Join to {roomIndex} Token:{clientInfo.token} Id:{clientInfo.id} Room:{clientInfo.room} Index:{clientInfo.index}");
            transmitter.SendRequestToServer(MessageType.Join, sendBuffer.Bytes, sendBuffer.Length, delayFactor, (error, buffer) =>
            {
                if (error == Error.NoError)
                {
                    clientInfo.room = buffer.ReadShort();
                    clientInfo.index = buffer.ReadSbyte();
                    Flag = (Flag)buffer.ReadByte();
                    callback?.Invoke(error, clientInfo.room, clientInfo.index);
                }
                else callback?.Invoke(error, -1, -1);

                Debug.Log($"{logName} Join to {roomIndex} response Token:{clientInfo.token} Id:{clientInfo.id} Room:{clientInfo.room} Index:{clientInfo.index}");
            });
        }

        public void LeaveRoom(System.Action<Error> callback)
        {
            if (clientInfo.device == null || clientInfo.token == 0) return;

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Leave)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(clientInfo.room)
                .AppendSbyte(clientInfo.index);

            Debug.Log($"{logName} Leave Token:{clientInfo.token} Id:{clientInfo.id} Room:{clientInfo.room} Index:{clientInfo.index}");
            transmitter.SendRequestToServer(MessageType.Leave, sendBuffer.Bytes, sendBuffer.Length, delayFactor, (error, buffer) =>
            {
                clientInfo.room = -1;
                clientInfo.index = -1;
                callback?.Invoke(error);

                Debug.Log($"{logName} Leave response Token:{clientInfo.token} Id:{clientInfo.id} Room:{clientInfo.room} Index:{clientInfo.index}");
            });
        }

        public void SendUnreliable(Target target, BufferWriter data, sbyte targetId = 0)
        {
            if (clientInfo.device == null || clientInfo.token == 0) return;
            transmitter.SendMessageUnreliable(target, data.Bytes, (byte)data.Length, targetId);
        }

        public void SendReliable(sbyte targetId, BufferWriter data)
        {
            if (clientInfo.device == null || clientInfo.token == 0) return;
            transmitter.SendMessageReliable(targetId, data.Bytes, (byte)data.Length);
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