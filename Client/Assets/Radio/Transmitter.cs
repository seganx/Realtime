using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace SeganX.Realtime.Internal
{
    public class Transmitter
    {
        private const string logName = "[Network] [Transmitter]";

        private ClientInfo clientInfo = null;
        private readonly Socket socket = new Socket();

        private Pool<RequestMessage> requestsPool = null;
        private IPEndPoint serverAddress = new IPEndPoint(0, 0);
        private readonly BufferWriter sendBuffer = new BufferWriter(512);
        private readonly BufferReader receivedBuffer = new BufferReader(512);

        private Reliable reliable = null;
        private System.Action<Error, sbyte, BufferReader, byte> OnReceivedMessage = null;

        public Transmitter Start(ClientInfo clientInfo, IPEndPoint serverAddress, System.Action<Error, sbyte, BufferReader, byte> OnReceivedMessage)
        {
            this.clientInfo = clientInfo;
            this.serverAddress = serverAddress;
            this.OnReceivedMessage = OnReceivedMessage;

            requestsPool = new Pool<RequestMessage>(32);
            reliable = new Reliable(socket, serverAddress, clientInfo, OnReceivedMessage);

            socket.Open(31001, 34999);
            return this;
        }

        public void Stop()
        {
            OnReceivedMessage = null;
            requestsPool.Clear();
            socket.Close();
        }

        public void SendRequestToServer(MessageType messageType, byte[] data, int dataSize, float retryDelay, System.Action<Error, BufferReader> callback)
        {
            var request = requestsPool.Find(x => x.type == messageType);
            if (request == null)
                request = requestsPool.Peek();

            request.type = messageType;
            request.maxDelayTime = retryDelay;
            request.delayTime = 0;
            request.callback = callback;
            request.dataSize = dataSize;
            System.Buffer.BlockCopy(data, 0, request.data, 0, dataSize);

            socket.Send(serverAddress, request.data, request.dataSize);

            if (messageType != MessageType.Ping)
                Debug.Log($"{logName} Sent request to server Type:{messageType} Size:{dataSize}");
        }

        public void SendMessageReliable(sbyte targetIndex, byte[] data, byte dataSize, int retryCount = 20, float retryDelay = 0.5f)
        {
            reliable?.SendReliable(targetIndex, data, dataSize, retryCount, retryDelay);
        }

        public void SendMessageUnreliable(Target targetType, byte[] data, byte dataSize, sbyte otherIndex = -1)
        {
            if (dataSize > 235)
            {
                Debug.LogError("[Network] Data length must be lees that 230 byes");
                return;
            }

            sbyte target = 0;
            switch (targetType)
            {
                case Target.All: target = -2; break;
                case Target.Other: target = -1; break;
                case Target.Player: target = otherIndex; break;
            }

            sendBuffer.Reset()
                .AppendByte((byte)MessageType.Unreliable)
                .AppendUint(clientInfo.token)
                .AppendShort(clientInfo.id)
                .AppendShort(clientInfo.room)
                .AppendSbyte(clientInfo.index)
                .AppendSbyte(target)
                .AppendByte(dataSize)
                .AppendBytes(data, dataSize);

            socket.Send(serverAddress, sendBuffer.Bytes, sendBuffer.Length);
        }

        public void Update(float elapsedTime)
        {
            if (OnReceivedMessage == null) return;

            while (Receive()) ;

            for (int i = 0; i < requestsPool.Count; i++)
                SendRequest(requestsPool[i], elapsedTime);

            reliable.Update(elapsedTime);
        }

        private bool Receive()
        {
            if (OnReceivedMessage == null) return false;

            var packsize = socket.Receive(receivedBuffer.Bytes);
            if (packsize < 1) return false;

            receivedBuffer.Reset();
            MessageType messageType = (MessageType)receivedBuffer.ReadByte();
            if ((byte)messageType < 1) return true;

            var sender = receivedBuffer.ReadSbyte();
            var error = sender < 0 ? (Error)sender : Error.NoError;

            if (error == Error.Expired)
                clientInfo.token = 0;

            switch (messageType)
            {
                case MessageType.Unreliable: OnReceivedMessage(error, sender, receivedBuffer, receivedBuffer.ReadByte()); break;
                case MessageType.Reliable: reliable.ReceivedReliable(error, sender, receivedBuffer); break;
                case MessageType.Relied: reliable.ReceivedRelied(error, sender, receivedBuffer); break;
                default: ReceivedRequest(messageType, error); break;
            }

            return true;
        }

        private void ReceivedRequest(MessageType messageType, Error error)
        {
            var request = requestsPool.Find(x => x.type == messageType);
            if (request == null) return;

            if (messageType != MessageType.Ping)
                Debug.Log($"{logName} Received response from server Type:{messageType} Error:{error}");

            request.type = 0;
            request.callback?.Invoke(error, receivedBuffer);
            request.callback = null;
        }

        private void SendRequest(RequestMessage request, float elapsedTime)
        {
            if (request.type == 0) return;

            request.delayTime += elapsedTime;
            if (request.delayTime < request.maxDelayTime) return;
            request.delayTime = 0;

            socket.Send(serverAddress, request.data, request.dataSize);
        }
    }
}