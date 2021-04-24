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

    void Start()
    {
        Network.serverAddress.Address = new IPAddress(new byte[] { 127, 0, 0, 1 });
        Network.serverAddress.Port = 31000;
    }

    private void Update()
    {
        int received = Network.Receive(buffer);
        if (received > 0)
        {
            var str = "Received:";
            for (int i = 0; i < received; i++)
                str += " " + buffer[i];
            Debug.Log(str);
        }
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 50, 50), "login"))
        {
            Network.Login(System.Text.Encoding.ASCII.GetBytes(device));
        }
    }
}
