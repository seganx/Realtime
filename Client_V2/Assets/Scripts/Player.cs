using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SeganX.Network;

public class Player : MonoBehaviour
{
    public enum MessageType : byte { Info = 1, Position = 2 }

    private NetPlayer netPlayer;
    private byte colorIndex = 0;

    public float speed = 2;

    public Vector3 Position { get; set; }

    public Player Setup(NetPlayer player)
    {
        netPlayer = player;
        netPlayer.OnReceived += (buffer, size) =>
        {
            var type = (MessageType)buffer.ReadByte();
            switch (type)
            {
                case MessageType.Position:
                    Position = buffer.ReadVector3();
                    break;

                case MessageType.Info:
                    colorIndex = buffer.ReadByte();
                    ChangeColor(colorIndex);
                    break;
            }
        };

        return this;
    }

    private void Start()
    {
        ChangeColor(colorIndex);
        StartCoroutine(SendPosition());
        StartCoroutine(SendInfo());
    }

    private void Update()
    {
        if (netPlayer.IsMine || Vector3.Distance(transform.position, Position) < 2)
            transform.position = Vector3.Lerp(transform.position, Position, Time.deltaTime * 5);
        else
            transform.position = Position;

#if UNITY_STANDALONE_WIN
        if (netPlayer.IsMine)
        {
            if (Input.GetKey(KeyCode.UpArrow))
                Position += Vector3.up * Time.deltaTime * speed;
            if (Input.GetKey(KeyCode.DownArrow))
                Position += Vector3.down * Time.deltaTime * speed;
            if (Input.GetKey(KeyCode.LeftArrow))
                Position += Vector3.left * Time.deltaTime * speed;
            if (Input.GetKey(KeyCode.RightArrow))
                Position += Vector3.right * Time.deltaTime * speed;
        }
#endif
    }

    private IEnumerator SendPosition()
    {
        BufferWriter buffer = new BufferWriter(128);
        var wait = new WaitForSeconds(0.1f);
        while (netPlayer.IsMine)
        {
            yield return wait;
            if (stopSend) continue;
            buffer.Reset();
            buffer.AppendByte((byte)MessageType.Position);
            buffer.AppendVector3(Position);
            netPlayer.SendUnreliable(Target.Other, buffer);
        }
    }

    private IEnumerator SendInfo()
    {
        var wait = new WaitForSeconds(1);
        while (true)
        {
            yield return wait;
            if (stopSend) continue;

            if (netPlayer.IsOther)
            {
                if (netPlayer.IsActive)
                    ChangeColor(colorIndex);
                else
                    GetComponent<MeshRenderer>().material.color = Color.black;
            }
        }
    }

    public void ChangeColor(int index)
    {
        if (index < 0) index = ++colorIndex;
        GetComponent<MeshRenderer>().material.color = colors[index % colors.Length];

        if (netPlayer.IsMine)
        {
            BufferWriter buffer = new BufferWriter(128);
            buffer.AppendByte((byte)MessageType.Info);
            buffer.AppendByte(colorIndex);
            netPlayer.SendReliable(Target.Other, buffer);
        }
    }

    //////////////////////////////////////////////////////
    /// STATIC MEMBERS
    //////////////////////////////////////////////////////
    private static readonly Color[] colors = { Color.green, Color.blue, Color.red, Color.cyan, Color.gray, Color.magenta, Color.yellow, Color.white };

    public static Player mine = null;
    public static readonly List<Player> all = new List<Player>(32);
    public static bool stopSend = false;

    public static void CreatePlayer(NetPlayer netPlayer)
    {
        Debug.Log($"Created network player: {netPlayer}");

        var prefab = Resources.Load<Player>("Game/Player");
        var res = Instantiate(prefab).GetComponent<Player>().Setup(netPlayer);
        all.Add(res);
        if (netPlayer.IsMine)
            mine = res;
    }

    public static void DestroyPlayer(NetPlayer player)
    {
        Debug.Log($"Destroyed network player: {player}");

        var candid = all.Find(x => x.netPlayer == player);
        if (candid == null) return;
        all.Remove(candid);
        Destroy(candid.gameObject);
    }
}
