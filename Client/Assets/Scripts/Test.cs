using UnityEngine;
using SeganX.Realtime;

public class Test : MonoBehaviour
{
    public string serverAddress = "79.175.133.132:31000";
    public string testAddress = "127.0.0.1:35000";

    private NetView currentView = null;

    private void Start()
    {
        Application.runInBackground = true;
        Radio.DebugDelayFactor = 1000;
        Radio.ConnectionTimeout = 500;
        Radio.PlayerActiveTimeout = 5;
        Radio.PlayerDestoryTimeout = 20;
        Radio.OnPlayerConnected += Player.CreatePlayer;
        Radio.OnPlayerRemoved += Player.DestroyPlayer;
        Radio.OnError += error => Debug.LogError("Net Error: " + error);
        Radio.OnMasterChanged += () => Debug.Log("Room's master has been changed!");

        NetView.OnViewCreated += (view, data, size) =>
        {
            var buffer = new BufferReader(data);
            Debug.Log($"New view created: {view} | {buffer.ReadString()}");
            currentView = view;
            currentView.OnReceived += CurrentView_OnReceived;
        };
    }

    private void OnDisable()
    {
        Radio.Disconnect(null);
        Radio.OnPlayerConnected -= Player.CreatePlayer;
        Radio.OnPlayerRemoved -= Player.DestroyPlayer;
    }

    void OnGUI()
    {
        Rect rect = new Rect(10, 10, 300, 30);
        GUI.Label(rect, $"Connection: {Radio.IsConnected}");
        rect.y += 20;
        GUI.Label(rect, $"Token:{Radio.Token} Room:{Radio.RoomId} Id:{Radio.PlayerId} IsMaster:{Radio.IsMaster}");
        rect.y += 20;
        GUI.Label(rect, $"ServerTime:{Radio.ServerTime} Ping:{Radio.Ping}");

        rect.width = 100;
        rect.y += 20;
        if (GUI.Button(rect, "Start"))
#if UNITY_STANDALONE_WIN
            Radio.Connect(testAddress, System.Text.Encoding.ASCII.GetBytes(ComputeMD5(SystemInfo.deviceUniqueIdentifier + System.DateTime.Now.Ticks, "sajad")), () => Debug.Log("Radio has been connected!"));
#else
            Plankton.Start(serverAddress, System.Text.Encoding.ASCII.GetBytes(ComputeMD5(SystemInfo.deviceUniqueIdentifier, "sajad")));
#endif

        rect.y += 40;
        if (GUI.Button(rect, "End"))
            Radio.Disconnect(() => Debug.Log("Radio has been disconnected!"));

        rect.y += 40;
        if (GUI.Button(rect, "Create Room"))
        {
            var properties = System.Text.Encoding.ASCII.GetBytes("12345678901234567890123456789012");
            var matchmaking = new MatchmakingParams { a = 1 };
            Radio.CreateRoom(System.DateTime.Now.ToString(), 1000, properties, matchmaking, (roomid, playerid) => Debug.Log($"Joined: Room[{roomid}] - Player[{playerid}]"));
        }

        rect.y += 40;
        if (GUI.Button(rect, "Join Room"))
        {
            var matchmaking = new MatchmakingRanges { aMin = 0, aMax = 4 };
            Radio.JoinRoom(System.DateTime.Now.ToString(), matchmaking, (roomid, playerid, properties) => Debug.Log($"Joined: Room[{roomid}] - Player[{playerid}] - Properties{System.Text.Encoding.ASCII.GetString(properties)}"));
        }

        rect.y += 40;
        if (GUI.Button(rect, "Leave Room"))
            Radio.LeaveRoom(() =>
            {
                Debug.Log($"Leaved room");
            });

        rect.y += 40;
        if (GUI.Button(rect, "Create View"))
        {
            var buffer = new BufferWriter(128);
            buffer.AppendString("Hello world!");
            NetView.CreateView(buffer);
        }

        rect.y += 40;
        if (GUI.Button(rect, "View send unrelibale"))
        {
            currentView?.Send(63, false, new BufferWriter(32).AppendString("unrelibale"));
        }

        rect.y += 40;
        if (GUI.Button(rect, "View send relibale"))
        {
            currentView?.Send(93, true, new BufferWriter(32).AppendString("hello"));
        }

#if !UNITY_STANDALONE_WIN
        rect.y += 80;
        rect.width = 40;
        if (GUI.Button(rect, "up"))
            Player.mine.Position += Vector3.up * 0.2f;

        rect.y += 40;
        if (GUI.Button(rect, "left"))
            Player.mine.Position += Vector3.left * 0.2f;

        rect.x += 50;
        if (GUI.Button(rect, "right"))
            Player.mine.Position += Vector3.right * 0.2f;
        rect.x -=  50;

        rect.y += 40;
        if (GUI.Button(rect, "down"))
            Player.mine.Position += Vector3.down * 0.2f;
#endif
        rect.y += 40;
        if (GUI.Button(rect, "Color"))
            Player.mine.ChangeColor(-1);

        rect.y += 40;
        if (GUI.Button(rect, "Stop Send"))
            Player.stopSend = !Player.stopSend;

    }

    private void CurrentView_OnReceived(byte code, byte[] data, byte size)
    {
        Debug.Log($"view received code:{code} data:{data} size:{size}");
    }



    //////////////////////////////////////////////////////
    /// STATIC MEMBERS
    //////////////////////////////////////////////////////
    public static string ComputeMD5(string str, string salt)
    {
        var md5 = System.Security.Cryptography.MD5.Create();
        byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(str + salt);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        var res = new System.Text.StringBuilder();
        for (int i = 0; i < hashBytes.Length; i++)
            res.Append(hashBytes[i].ToString("X2"));
        return res.ToString();
    }
}
