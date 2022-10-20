using UnityEngine;
using System.Net;
using System.Net.Sockets;

namespace SeganX.Realtime.Internal
{
    public class Socket
    {
        private UdpClient udpClient = null;

        private EndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        // open a udp socket on a port in range and return the port number
        public int Open(int minPort, int maxPort)
        {
            for (int port = minPort; port <= maxPort; port++)
            {
                try
                {
                    Close();
                    udpClient = new UdpClient(port);
                    udpClient.Client.Blocking = false;
                    return port;
                }
                catch { }
            }
            return 0;
        }

        public void Close()
        {
            if (udpClient != null)
                udpClient.Close();
        }

        public bool Send(IPEndPoint destination, byte[] buffer, int size)
        {
            if (udpClient == null || buffer == null || buffer.Length < size || size < 1)
            {
                Debug.LogError("Send failed!");
                return false;
            }

            try
            {
                int sentBytes = udpClient.Send(buffer, size, destination);
                return sentBytes == size;
            }
            catch
            {
                return false;
            }
        }

        public int Receive(byte[] buffer)
        {
            if (udpClient.Client.Available < 1) return 0;

            try
            {
                return udpClient.Client.ReceiveFrom(buffer, ref anyIP);
            }
            catch { };

            return 0;
        }
    }
}