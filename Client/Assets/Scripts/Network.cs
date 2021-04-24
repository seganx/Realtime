using UnityEngine;
using UnityEngine.Networking;
using System.Net;
using System.Threading.Tasks;

public static class Network
{
    [System.Serializable]
    public class IpInfo
    {
        public string ip = string.Empty;
    }

    private static NetSocket socket = new NetSocket();
    private static IPEndPoint selfAddress = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0);

    public static IPEndPoint serverAddress = new IPEndPoint(0, 0);

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

    public static int Receive(byte[] buffer)
    {
        return socket.Receive(buffer);
    }

    public static void Login(byte[] devicebytes)
    {
        if (devicebytes.Length != 32)
        {
            Debug.LogError("[Network] Login: Device id must be 32 bytes");
        }

        var buffer = new Buffer(256);
        buffer.AppendByte(2);
        buffer.AppendBytes(selfAddress.Address.GetAddressBytes());
        buffer.AppendUshort((ushort)selfAddress.Port);
        buffer.AppendBytes(devicebytes);
        buffer.AppendUint(ComputeChecksum(buffer.Bytes, buffer.Length));
        socket.Send(serverAddress, buffer.Bytes, buffer.Length);
    }

    private static uint ComputeChecksum(byte[] buffer, int length)
    {
        uint checksum = 0;
        for (int i = 0; i < length; i++)
            checksum += 64548 + (uint)buffer[i] * 6597;
        return checksum;
    }



#if OFF
    public byte[] NetHeaderToByteArray(NetHeader header)
    {
        byte[] buffer = new byte[NET_HEADER_SIZE];
        Buffer.BlockCopy(BitConverter.GetBytes(header.netId), 0, buffer, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(header.number), 0, buffer, 2, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(header.option), 0, buffer, 4, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(header.checksum), 0, buffer, 6, 2);
        return buffer;
    }

    public NetHeader ByteArrayToNetHeader(byte[] data)
    {
        NetHeader nh = new NetHeader();
        nh.netId = BitConverter.ToUInt16(data, 0);
        nh.number = BitConverter.ToUInt16(data, 2);
        nh.option = BitConverter.ToUInt16(data, 4);
        nh.checksum = BitConverter.ToUInt16(data, 6);
        return nh;
    }

    public ushort ComputeChecksum(byte[] buffer)
    {
        ushort res = NET_ID;
        for (int i = 0; i < buffer.Length; ++i)
            res += (ushort)((res + buffer[i]) * NET_ID);
        return res;
    }

    public bool VerifyPackage(byte[] buffer, int size, ulong lastNumber)
    {
        //	validate message size
        if (size < NET_HEADER_SIZE || size > NET_BUFF_SIZE)
            return false;

        NetHeader nh = ByteArrayToNetHeader(buffer);

        //	validate net id
        if (nh.netId != NET_ID)
            return false;

        //	validate if message is duplicated
        //if ( nh.number == lastNumber && set_hasnt( nh.option, NET_OPTN_SAFESEND ) )
        //    return false;

        //	validate data checksum
        if (size > NET_HEADER_SIZE)
        {
            if (nh.checksum != ComputeChecksum(buffer))
                return false;
        }

        // new we can suppose that the package is valid
        return true;
    }

    void Update()
    {
        PeekReceivedMessages();
    }

    void OnApplicationQuit()
    {
        //rcvThread.Abort();
    }

    void PeekReceivedMessages()
    {
        byte[] buffer = new byte[NET_BUFF_SIZE];

        int receivedBytes;
        do
        {
            IPEndPoint address = new IPEndPoint(IPAddress.Any, localPort);

            //	peek the package
            receivedBytes = socket.Receive(ref buffer, ref address);
            if (receivedBytes < 1) continue;

            Thread.Sleep(100);
        }
        while (receivedBytes > 0);
    }
#endif
}

