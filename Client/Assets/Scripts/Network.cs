using UnityEngine;
using UnityEngine.Networking;
using System.Net;
using System.Threading.Tasks;

public static class Network
{
    public enum SendType : byte
    {
        All = 1,
        Other = 2,
        Player = 3
    }

    [System.Serializable]
    public class IpInfo
    {
        public string ip = string.Empty;
    }

    private const byte packetTypePing = 1;
    private const byte packetTypeLogin = 2;
    private const byte packetTypeLogout = 3;
    private const byte packetTypeMessage = 4;

    private static float pingTime = -10;
    private static byte[] device = null;
    private static readonly long[] time = { 0 };
    private static readonly uint[] token = { 0 };
    private static readonly ushort[] roomId = { 0 };
    private static readonly byte[] playerId = { 0 };
    private static readonly byte[] senderId = { 0 };
    private static readonly uint[] checksum = { 0 };
    private static readonly byte[] receivedBuffer = new byte[256];
    private static readonly Buffer loginBuffer = new Buffer(64);
    private static readonly Buffer sendBuffer = new Buffer(256);
    private static readonly Buffer pingBuffer = new Buffer(16);
    private static readonly NetSocket socket = new NetSocket();
    private static readonly IPEndPoint selfAddress = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0);

    private static long Ticks => System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond;

    public static IPEndPoint ServerAddress = new IPEndPoint(0, 0);
    public static byte PlayerId => playerId[0];
    public static ushort RoomId => roomId[0];
    public static long Ping { get; private set; } = 0;
    public static bool Connected => (Time.time - pingTime) < 10.0f;

    public static uint Token
    {
        get => token[0];
        private set => token[0] = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RuntimeInitialize()
    {
        selfAddress.Port = socket.Open(32000, 35000);
#if !UNITY_EDITOR
        UpdateAddress();
#endif
    }

    private static async void UpdateAddress()
    {
        float startTime = Time.time;
        var web = UnityWebRequest.Get("https://ipinfo.io/json").SendWebRequest();
        while (web.isDone == false || Time.time - startTime > 10) await Task.Delay(10);

        if (web.isDone == false)
        {
            web = UnityWebRequest.Get("http://seganx.ir/games/api/ip.php").SendWebRequest();
            while (web.isDone == false || Time.time - startTime > 10) await Task.Delay(10);
        }

        try
        {
            var ipinfo = JsonUtility.FromJson<IpInfo>(web.webRequest.downloadHandler.text);
            selfAddress.Address = IPAddress.Parse(ipinfo.ip);
            Debug.Log("[Network] Self address: " + selfAddress.Address + ":" + selfAddress.Port);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public static void Start(byte[] devicebytes)
    {
        if (devicebytes.Length != 32)
        {
            Debug.LogError("[Network] Login: Device id must be 32 bytes");
            return;
        }
        device = devicebytes;
        pingTime = -10;
        Login();
        Pinger();
    }

    private static async void Login()
    {
        while (Token == 0)
        {
            loginBuffer.Reset();
            loginBuffer.AppendByte(packetTypeLogin);
            loginBuffer.AppendBytes(selfAddress.Address.GetAddressBytes(), 4);
            loginBuffer.AppendUshort((ushort)selfAddress.Port);
            loginBuffer.AppendBytes(device, 32);
            loginBuffer.AppendUint(ComputeChecksum(loginBuffer.Bytes, loginBuffer.Length));
            socket.Send(ServerAddress, loginBuffer.Bytes, loginBuffer.Length);
            await Task.Delay(1000);
        }
    }

    private static async void Pinger()
    {
        while (device != null)
        {
            pingBuffer.Reset();
            pingBuffer.AppendByte(packetTypePing);
            pingBuffer.AppendBytes(selfAddress.Address.GetAddressBytes(), 4);
            pingBuffer.AppendUshort((ushort)selfAddress.Port);
            pingBuffer.AppendLong(Ticks);
            socket.Send(ServerAddress, pingBuffer.Bytes, pingBuffer.Length);
            await Task.Delay(1000);
        }
    }


    public static void End()
    {
        if (device == null) return;
        sendBuffer.Reset();
        sendBuffer.AppendByte(packetTypeLogout);
        sendBuffer.AppendUshort(RoomId);
        sendBuffer.AppendByte(PlayerId);
        sendBuffer.AppendUint(Token);
        sendBuffer.AppendUint(ComputeChecksum(sendBuffer.Bytes, sendBuffer.Length));
        socket.Send(ServerAddress, sendBuffer.Bytes, sendBuffer.Length);

        Ping = 0;
        Token = 0;
        device = null;
        pingTime = -10;
    }

    public static void Send(SendType type, Buffer data, byte otherId = 0)
    {
        if (device == null || Token == 0) return;
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
        sendBuffer.AppendBytes(selfAddress.Address.GetAddressBytes(), 4);
        sendBuffer.AppendUshort((ushort)selfAddress.Port);
        sendBuffer.AppendUshort(RoomId);
        sendBuffer.AppendByte(PlayerId);
        sendBuffer.AppendUint(Token);
        sendBuffer.AppendByte(otherId);
        sendBuffer.AppendByte(option);
        sendBuffer.AppendByte((byte)data.Length);
        sendBuffer.AppendBytes(data.Bytes, data.Length);
        socket.Send(ServerAddress, sendBuffer.Bytes, sendBuffer.Length);

    }

    public static int Receive(ref int sender, byte[] destBuffer)
    {
        var packsize = socket.Receive(receivedBuffer);
        if (packsize < 1) return 0;

        switch (receivedBuffer[0])
        {
            case packetTypePing:
                {
                    System.Buffer.BlockCopy(receivedBuffer, 1, time, 0, sizeof(long));
                    Ping = (Ticks - time[0]);
                    pingTime = Time.unscaledTime;
                }
                break;

            case packetTypeLogin:
                {
                    System.Buffer.BlockCopy(receivedBuffer, 8, checksum, 0, 4);
                    if (checksum[0] == ComputeChecksum(receivedBuffer, 8))
                    {
                        System.Buffer.BlockCopy(receivedBuffer, 1, roomId, 0, 2);
                        System.Buffer.BlockCopy(receivedBuffer, 3, playerId, 0, 1);
                        System.Buffer.BlockCopy(receivedBuffer, 4, token, 0, 4);
                    }
                    return 0;
                }

            case packetTypeMessage:
                {
                    System.Buffer.BlockCopy(receivedBuffer, 1, senderId, 0, 1);
                    System.Buffer.BlockCopy(receivedBuffer, 2, destBuffer, 0, packsize - 2);
                    sender = senderId[0];
                    return packsize - 2;
                }
        }
        return 0;
    }

    private static uint ComputeChecksum(byte[] buffer, int length)
    {
        uint checksum = 0;
        for (int i = 0; i < length; i++)
            checksum += 64548 + (uint)buffer[i] * 6597;
        return checksum;
    }
}

