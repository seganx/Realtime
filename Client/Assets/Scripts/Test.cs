using System;
using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;

public class Test : MonoBehaviour
{
    [TextArea]
    public string device = "12345678901234567890123456789012";
    [TextArea]
    public string text = "salam";


    byte[] buffer = new byte[256];

    private void Start()
    {
        Network.ServerAddress.Address = new IPAddress(new byte[] { 127, 0, 0, 1 });
        Network.ServerAddress.Port = 31000;
    }

    private void OnApplicationQuit()
    {
        Network.End();
    }

    private void Update()
    {
        int senderId = -1;
        int received = Network.Receive(ref senderId, buffer);
        if (received > 0)
        {
            var str = "Received from " + senderId + " : ";
            for (int i = 0; i < received; i++)
                str += " " + buffer[i];
            Debug.Log(str);
        }
    }

    void OnGUI()
    {
        float y = 10;
        GUI.Label(new Rect(10, y, 150, 30), "Connection: " + Network.Connected);
        y += 30;
        GUI.Label(new Rect(10, y, 150, 30), "Ping: " + Network.Ping);

        y += 30;
        if (GUI.Button(new Rect(10, y, 150, 30), "Start"))
        {
            Network.Start(System.Text.Encoding.ASCII.GetBytes(device));
        }

        y += 40;
        if (GUI.Button(new Rect(10, y, 150, 30), "End"))
        {
            Network.End();
        }

        y += 40;
        if (GUI.Button(new Rect(10, y, 150, 30), "Send All"))
        {
            var tmp = new Buffer(230);
            tmp.AppendString(text);
            Network.Send(Network.SendType.All, tmp);
        }
    }
}
