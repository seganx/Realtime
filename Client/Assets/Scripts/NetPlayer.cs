using System;
using System.Collections.Generic;

public class NetPlayer
{
    public int Id { get; private set; } = 0;

    public bool IsMine => Network.PlayerId == Id;
    public bool IsOther => Network.PlayerId != Id;

    public Action OnReceived = () => { };

    private NetPlayer(int id)
    {
        Id = id;
    }

    public void Send(Network.SendType type, BufferWriter data, byte otherId = 0)
    {
        if (IsOther) return;
        Network.Send(type, data, otherId);
    }

    //////////////////////////////////////////////////////
    /// STATIC MEMBERS
    //////////////////////////////////////////////////////
    public static readonly List<NetPlayer> all = new List<NetPlayer>();

    public static NetPlayer Create(int id)
    {
        var res = new NetPlayer(id);
        all.Add(res);
        return res;
    }

    public static void Destroy(NetPlayer player)
    {
        all.Remove(player);
    }
}
