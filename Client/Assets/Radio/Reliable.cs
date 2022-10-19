using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace SeganX.Network.Internal
{
    public class Reliable
    {
        private const string logName = "[Network] [Reliable]";

        private Socket socket = null;
        private ClientInfo clientInfo = null;
        private IPEndPoint serverAddress = null;
        private readonly BufferWriter sendBuffer = new BufferWriter(512);
        private Action<Error, sbyte, BufferReader, byte> OnReceivedMessage = null;

        private byte AckNumber = 0;
        private readonly List<int> AcksCache = new List<int>(64);
        private readonly List<ReliableMessage> ReadyMessages = new List<ReliableMessage>(128);
        private readonly List<ReliableMessage> SendingMessages = new List<ReliableMessage>(64);
        private readonly Pool<ReliableMessage> messagePool = new Pool<ReliableMessage>(128);

        public Reliable(Socket socket, IPEndPoint serverAddress, ClientInfo clientInfo, Action<Error, sbyte, BufferReader, byte> OnReceivedMessage)
        {
            this.socket = socket;
            this.clientInfo = clientInfo;
            this.serverAddress = serverAddress;
            this.OnReceivedMessage = OnReceivedMessage;

            while (AcksCache.Count < AcksCache.Capacity)
                AcksCache.Add(-1);
        }

        public void SendReliable(sbyte targetIndex, byte[] data, byte dataSize, int retryCount = 10, float retryDelay = 0.5f)
        {
            if (dataSize > 235)
            {
                Debug.LogError($"{logName} Data length must be lees that 235 byes");
                return;
            }

            while (++AckNumber == 0) ;

            var message = messagePool.Peek();
            message.maxDelayTime = retryDelay;
            message.delayTime = 0;
            message.ack = AckNumber;
            message.targetIndex = targetIndex;
            message.retryCount = retryCount;
            message.buffer.Reset()
                .AppendByte((byte)MessageType.Reliable)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(clientInfo.room)
                .AppendSbyte(clientInfo.index)
                .AppendSbyte(targetIndex)
                .AppendByte(AckNumber)
                .AppendByte(dataSize)
                .AppendBytes(data, dataSize);

            if (SendingMessages.AddUnique(message, x => x.targetIndex == targetIndex))
                socket.Send(serverAddress, message.buffer.Bytes, message.buffer.Length);
            else
                ReadyMessages.Add(message);

            //Debug.Log($"{logName} Send Reliable Target:{targetIndex} Ack:{AckNumber}");
        }

        public void Update(float elapsedTime)
        {
            for (int i = 0; i < ReadyMessages.Count; i++)
                if (SendingMessages.AddUnique(ReadyMessages[i], x => x.targetIndex == ReadyMessages[i].targetIndex))
                    ReadyMessages.RemoveAt(i--);

            for (int i = 0; i < SendingMessages.Count; i++)
                SendReliable(SendingMessages[i], elapsedTime);
        }

        public void ReceivedReliable(Error error, sbyte sender, BufferReader receivedBuffer)
        {
            if (error != Error.NoError)
            {
                OnReceivedMessage(error, 0, receivedBuffer, 0);
                return;
            }
            if (sender < 0 || sender >= AcksCache.Capacity) return;

            byte ack = receivedBuffer.ReadByte();
            //Debug.Log($"{logName} Received Reliable Sender:{sender} Ack:{ack}");

            SendRelied(sender, ack);

            if (AcksCache[sender] == ack) return;
            AcksCache[sender] = ack;

            var datasize = receivedBuffer.ReadByte();
            OnReceivedMessage(Error.NoError, sender, receivedBuffer, datasize);
        }

        public void ReceivedRelied(Error error, sbyte sender, BufferReader receivedBuffer)
        {
            if (error != Error.NoError)
            {
                OnReceivedMessage(error, 0, receivedBuffer, 0);
                return;
            }
            byte ack = receivedBuffer.ReadByte();

            //Debug.Log($"{logName} Received Relied Sender:{sender} Ack:{ack}");

            var message = SendingMessages.Find(x => x.ack == ack && x.targetIndex == sender);
            if (message != null)
                SendingMessages.Remove(message);
        }

        private void SendReliable(ReliableMessage message, float elapsedTime)
        {
            message.delayTime += elapsedTime;
            if (message.delayTime < message.maxDelayTime) return;
            message.delayTime = 0;

            if (message.retryCount > 0)
            {
                socket.Send(serverAddress, message.buffer.Bytes, message.buffer.Length);
                message.retryCount--;

                //Debug.Log($"{logName} Send Reliable Target:{message.targetIndex} Ack:{message.ack}");
            }
            else SendingMessages.Remove(message);
        }

        private void SendRelied(sbyte sender, byte ack)
        {
            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Relied)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(clientInfo.room)
                .AppendSbyte(clientInfo.index)
                .AppendSbyte(sender)
                .AppendByte(ack);
            socket.Send(serverAddress, sendBuffer.Bytes, sendBuffer.Length);

            //Debug.Log($"{logName} Send Relied Target:{sender} Ack:{ack}");
        }
    }
}