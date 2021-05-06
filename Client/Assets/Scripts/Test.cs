using System;
using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public class Test : MonoBehaviour
{
    [SerializeField] private Player player = null;

    private byte[] buffer = new byte[256];

    private void Start()
    {
        Application.runInBackground = true;
        Network.ServerAddress.Address = new IPAddress(new byte[] { 79, 175, 133, 132 });
        Network.ServerAddress.Port = 31000;
    }

    private void OnApplicationQuit()
    {
        Network.End();
    }

    private void Update()
    {
        player.id = Network.PlayerId;

        int senderId = -1;
        int received = Network.Receive(ref senderId, buffer);
        if (received > 0)
        {
            Player.Received(senderId, buffer);
#if UNITY_EDITOR
            var str = "Received from " + senderId + " : ";
            for (int i = 0; i < received; i++)
                str += " " + buffer[i];
            Debug.Log(str);
#endif
        }
    }

    void OnGUI()
    {
        Rect rect = new Rect(10, 10, 150, 30);
        GUI.Label(rect, "Connection: " + Network.Connected);
        rect.y += 20;
        GUI.Label(rect, "Ping: " + Network.Ping);

        rect.y += 30;
        if (GUI.Button(rect, "Start"))
#if UNITY_STANDALONE_WIN
            Network.Start(System.Text.Encoding.ASCII.GetBytes(ComputeMD5(SystemInfo.deviceUniqueIdentifier + System.DateTime.Now.Ticks, "sajad")));
#else
            Network.Start(System.Text.Encoding.ASCII.GetBytes(ComputeMD5(SystemInfo.deviceUniqueIdentifier, "sajad")));
#endif

        rect.y += 40;
        if (GUI.Button(rect, "End"))
            Network.End();

#if UNITY_STANDALONE_WIN
        if (Input.GetKey(KeyCode.UpArrow))
            player.transform.position += Vector3.up * 0.01f;
        if (Input.GetKey(KeyCode.DownArrow))
            player.transform.position += Vector3.down * 0.01f;
        if (Input.GetKey(KeyCode.LeftArrow))
            player.transform.position += Vector3.left * 0.01f;
        if (Input.GetKey(KeyCode.RightArrow))
            player.transform.position += Vector3.right * 0.01f;
#else
        rect.y += 80;
        rect.width = 40;
        if (GUI.Button(rect, "up"))
            player.transform.position += Vector3.up * 0.2f;

        rect.y += 40;
        if (GUI.Button(rect, "left"))
            player.transform.position += Vector3.left * 0.2f;

        rect.x += 50;
        if (GUI.Button(rect, "right"))
            player.transform.position += Vector3.right * 0.2f;
        rect.x -=  50;

        rect.y += 40;
        if (GUI.Button(rect, "down"))
            player.transform.position += Vector3.down * 0.2f;
#endif
        rect.y += 40;
        if (GUI.Button(rect, "color"))
            player.ChangeColor(++player.colorIndex);

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
