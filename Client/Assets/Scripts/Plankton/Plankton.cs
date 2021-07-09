using System;
using System.Collections.Generic;
using UnityEngine;
using SeganX.Plankton;
using SeganX.Plankton.Net;
using System.Net;
using System.Collections;
using System.Threading.Tasks;

#if UNITY_EDITOR
using System.Reflection;
using System.Threading;
#endif

public class Plankton : MonoBehaviour
{
    private enum State { None, FetchingRooms, Joining, Leaving }

    public const int Capacity = 32;
    private readonly BufferReader buffer = new BufferReader(256);

    private IEnumerator Start()
    {
        var wait = new WaitForSecondsRealtime(1);
        while (true)
        {
            for (int i = 0; i < cache.Count; i++)
            {
                var player = cache[i];
                if (player == null || player.IsMine) continue;

                var deltaTime = Time.time - player.LastActiveTime;
                if (deltaTime > PlayerDestoryTimeout)
                {
                    cache[i] = null;
                    players.Remove(player);
                    player.OnDestory?.Invoke();
                    OnPlayerDestroyed?.Invoke(player);
                }
                else player.IsActive = deltaTime < PlayerActiveTimeout;
            }

            yield return wait;
        }
    }

    private void Update()
    {
        int senderId;
        do
        {
            senderId = -1;
            netlayer.Receive(ref senderId, buffer.Bytes);
            if (senderId >= 0 && senderId < cache.Count)
                ProcessReceivedData(senderId);
        }
        while (senderId >= 0);
    }

    private void ProcessReceivedData(int senderId)
    {
        buffer.Reset();

        var player = cache[senderId];

        if (player == null)
        {
            cache[senderId] = player = new NetPlayer(netlayer, senderId);
            players.Add(player);
            OnPlayerConnected?.Invoke(player);
        }

        player.OnReceived(buffer);
        player.LastActiveTime = Time.time;
    }

#if UNITY_EDITOR
    private void OnApplicationQuit()
    {
        var constructor = SynchronizationContext.Current.GetType().GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(int) }, null);
        var newContext = constructor.Invoke(new object[] { Thread.CurrentThread.ManagedThreadId });
        SynchronizationContext.SetSynchronizationContext(newContext as SynchronizationContext);
    }
#endif


    //////////////////////////////////////////////////////
    /// STATIC MEMBERS
    //////////////////////////////////////////////////////
    private static Plankton instance = null;
    private static readonly Netlayer netlayer = new Netlayer();
    private static readonly List<NetPlayer> cache = new List<NetPlayer>(Capacity);
    private static State state = State.None;

    public static readonly List<NetPlayer> players = new List<NetPlayer>(Capacity);
    public static event Action<NetPlayer> OnPlayerConnected = null;
    public static event Action<NetPlayer> OnPlayerDestroyed = null;

    public static float PlayerActiveTimeout { get; set; } = 15;
    public static float PlayerDestoryTimeout { get; set; } = 300;

    public static bool IsConnected => netlayer.Connected;
    public static long Ping => netlayer.Ping;
    public static sbyte PlayerId => netlayer.PlayerId;
    public static short RoomId => netlayer.RoomId;

    public static void Connect(string serverAddress, byte[] deviceId)
    {
        if (instance != null) return;

        for (int i = 0; i < cache.Capacity; i++)
            cache.Add(null);

        instance = new GameObject(nameof(Plankton)).AddComponent<Plankton>();
        //instance.gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(instance);

        var addressParts = serverAddress.Split(':');
        netlayer.ServerAddress.Address = IPAddress.Parse(addressParts[0]);
        netlayer.ServerAddress.Port = int.Parse(addressParts[1]);

        netlayer.OnLoggedIn = () => Debug.Log("Logged in...");

        netlayer.Start(deviceId);
    }

    public static void Disconnect()
    {
        if (instance == null) return;

        Destroy(instance.gameObject);
        netlayer.Stop();

        cache.Clear();
        foreach (var player in players)
        {
            player.OnDestory?.Invoke();
            OnPlayerDestroyed?.Invoke(player);
        }
        players.Clear();
    }

    public static async void GetRooms(short startRoomIndex, byte count, bool excludeFullRooms, Action<byte, byte[]> callback)
    {
        if (instance == null || netlayer == null || state != State.None) return;

        netlayer.OnRoomsReceived = (rcount, rooms) =>
        {
            state = State.None;
            callback?.Invoke(rcount, rooms);
        };

        state = State.FetchingRooms;
        while (state == State.FetchingRooms)
        {
            netlayer.GetRooms(startRoomIndex, count, excludeFullRooms);
            await Task.Delay(1000 * Netlayer.tryDelayFactor);
        }
    }

    public static async void JoinRoom(short roomIndex = -1, Action<short, sbyte> callback = null)
    {
        if (instance == null || netlayer == null || state != State.None) return;

        netlayer.OnJoinRoom = (roomId, playerId) =>
        {
            state = State.None;
            callback?.Invoke(roomId, playerId);
        };

        state = State.Joining;
        while (state == State.Joining)
        {
            netlayer.JoinRoom(roomIndex);
            await Task.Delay(1000 * Netlayer.tryDelayFactor);
        }
    }

    public static async void LeaveRoom(Action callback)
    {
        if (instance == null || netlayer == null || state != State.None) return;

        netlayer.OnLeaveRoom = () =>
        {
            state = State.None;
            callback?.Invoke();
        };

        state = State.Leaving;
        while (state == State.Leaving)
        {
            netlayer.LeaveRoom();
            await Task.Delay(1000 * Netlayer.tryDelayFactor);
        }
    }
}
