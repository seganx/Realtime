using UnityEngine;
using SeganX.Realtime;

public class Test : MonoBehaviour
{
    public string serverAddress = "79.175.133.132:31000";
    public string testAddress = "127.0.0.1:35000";

    private void Start()
    {
        Application.runInBackground = true;
        Radio.PlayerActiveTimeout = 5;
        Radio.PlayerDestoryTimeout = 20;
        Radio.OnPlayerConnected += Player.CreatePlayer;
        Radio.OnPlayerDestroyed += Player.DestroyPlayer;
        Radio.OnError += error => Debug.LogError("Net Error: " + error);
    }

    private void OnDisable()
    {
        Radio.Disconnect(null);
        Radio.OnPlayerConnected -= Player.CreatePlayer;
        Radio.OnPlayerDestroyed -= Player.DestroyPlayer;
    }

    void OnGUI()
    {
        Rect rect = new Rect(10, 10, 300, 30);
        GUI.Label(rect, $"Connection: {Radio.IsConnected}");
        rect.y += 20;
        GUI.Label(rect, $"Token:{Radio.Token} Room:{Radio.RoomId} Id:{Radio.PlayerId} IsMaster:{Radio.IsMaster}");
        rect.y += 20;
        GUI.Label(rect, $"Ping:{Radio.Ping} ServerTime:{Radio.ServerTime}");

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
            Radio.CreateRoom(1000, properties, matchmaking, (roomid, playerid) => Debug.Log($"Joined: Room[{roomid}] - Player[{playerid}]"));
        }


        rect.y += 40;
        if (GUI.Button(rect, "Join Room"))
        {
            var matchmaking = new MatchmakingRanges { aMin = 3, aMax = 4 };
            Radio.JoinRoom(matchmaking, (roomid, playerid, properties) => Debug.Log($"Joined: Room[{roomid}] - Player[{playerid}] - Properties{System.Text.Encoding.ASCII.GetString(properties)}"));
        }


        rect.x = 10; rect.width = 150;
        rect.y += 40;
        if (GUI.Button(rect, "Leave Room"))
            Radio.LeaveRoom(() =>
            {
                Debug.Log($"Leaved room");
            });


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
