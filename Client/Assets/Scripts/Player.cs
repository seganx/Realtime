using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public enum MessageType : byte { Info = 1, Position = 2 }

    public int id = 0;
    public byte colorIndex = 0;

    public NetPlayer netPlayer = null;

    private void OnEnable()
    {
        players.Add(this);
    }

    private void OnDestroy()
    {
        players.Remove(this);
    }

    private void Start()
    {
        ChangeColor(colorIndex);
        StartCoroutine(SendPosition());
        StartCoroutine(SendInfo());
    }

    private IEnumerator SendPosition()
    {
        BufferWriter buffer = new BufferWriter(128);
        var wait = new WaitForSeconds(0.1f);
        while (Network.PlayerId == id)
        {
            buffer.Reset();
            buffer.AppendByte((byte)MessageType.Position);
            buffer.AppendFloat(transform.position.x);
            buffer.AppendFloat(transform.position.y);
            buffer.AppendFloat(transform.position.z);
            Network.Send(Network.SendType.Other, buffer);
            yield return wait;
        }
    }

    private void ReceivedPosition(byte[] buffer)
    {
        float[] position = { 0, 0, 0 };
        System.Buffer.BlockCopy(buffer, 1, position, 0, 12);
        transform.position = new Vector3(position[0], position[1], position[2]);
    }

    private IEnumerator SendInfo()
    {
        BufferWriter buffer = new BufferWriter(128);
        var wait = new WaitForSeconds(1);
        while (Network.PlayerId == id)
        {
            buffer.Reset();
            buffer.AppendByte((byte)MessageType.Info);
            buffer.AppendByte(colorIndex);
            Network.Send(Network.SendType.Other, buffer);
            yield return wait;
        }
    }

    private void ReceivedInfo(byte[] buffer)
    {
        colorIndex = buffer[1];
        ChangeColor(colorIndex);
    }

    public void ChangeColor(int index)
    {
        GetComponent<MeshRenderer>().material.color = colors[index % colors.Length];
    }

    //////////////////////////////////////////////////////
    /// STATIC MEMBERS
    //////////////////////////////////////////////////////
    private static readonly List<Player> players = new List<Player>();
    private static Color[] colors = { Color.green, Color.blue, Color.red, Color.cyan, Color.gray };

    public static void Received(int senderId, byte[] buffer)
    {
        var player = players.Find(x => x.id == senderId);

        if (player == null)
        {
            player = Create(senderId);
        }

        var type = (MessageType)buffer[0];
        switch (type)
        {
            case MessageType.Info: player.ReceivedInfo(buffer); break;
            case MessageType.Position: player.ReceivedPosition(buffer); break;
        }
    }

    private static Player Create(int id)
    {
        var prefab = Resources.Load<Player>("Game/Player");
        var player = Instantiate(prefab).GetComponent<Player>();
        player.id = id;
        return player;
    }
}
