using UnityEngine;
using System.Net;
using SeganX.Plankton;

public class Test : MonoBehaviour
{
    public string serverAddress = "79.175.133.132:31000";
    public string testAddress = "127.0.0.1:35000";

    private void Start()
    {
        Application.runInBackground = true;
        Plankton.PlayerActiveTimeout = 5;
        Plankton.PlayerDestoryTimeout = 20;
        Plankton.OnPlayerConnected += Player.CreatePlayer;
        Plankton.OnPlayerDestroyed += Player.DestroyPlayer;
    }

    private void OnDisable()
    {
        Plankton.Disconnect();
        Plankton.OnPlayerConnected -= Player.CreatePlayer;
        Plankton.OnPlayerDestroyed -= Player.DestroyPlayer;
    }

    void OnGUI()
    {
        Rect rect = new Rect(10, 10, 150, 30);
        GUI.Label(rect, "Connection: " + Plankton.IsConnected);
        rect.y += 20;
        GUI.Label(rect, "Ping: " + Plankton.Ping);

        rect.y += 30;
        if (GUI.Button(rect, "Start"))
#if UNITY_STANDALONE_WIN
            Plankton.Connect(testAddress, System.Text.Encoding.ASCII.GetBytes(ComputeMD5(SystemInfo.deviceUniqueIdentifier + System.DateTime.Now.Ticks, "sajad")));
#else
            Plankton.Start(serverAddress, System.Text.Encoding.ASCII.GetBytes(ComputeMD5(SystemInfo.deviceUniqueIdentifier, "sajad")));
#endif

        rect.y += 40;
        if (GUI.Button(rect, "End"))
            Plankton.Disconnect();

        rect.y += 40;
        if (GUI.Button(rect, "Get Rooms"))
            Plankton.GetRooms(0, 10, true, (count, rooms) =>
            {
                string str = $"Rooms: Count[{count}] - Rooms: ";
                for (int i = 0; i < count; i++)
                    str += rooms[i] + " ";
                Debug.Log(str);
            });


        rect.y += 40;
        if (GUI.Button(rect, "Join Room 0"))
            Plankton.JoinRoom(0, (roomid, playerid) =>
            {
                Debug.Log($"Joined: Room[{roomid}] - Player[{playerid}]");
            });

        rect.y += 40;
        if (GUI.Button(rect, "Leave Room"))
            Plankton.LeaveRoom(() =>
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
