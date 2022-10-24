using System;
using UnityEngine;
using SeganX.Realtime.Internal;
using System.Net;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using System.Reflection;
using System.Threading;
#endif

namespace SeganX.Realtime
{
    public class Radio : MonoBehaviour
    {
        private const int maxPlayers = 16;
        private bool connectionState = false;
        private int taskOrder = 0;

        private IEnumerator Start()
        {
            var wait = new WaitForSecondsRealtime(1);
            while (true)
            {
                for (int i = 0; i < maxPlayers; i++)
                {
                    var player = players[i];
                    if (player == null || player.IsMine) continue;

                    var deltaTime = player.Update(PlayerActiveTimeout);
                    if (deltaTime > PlayerDestoryTimeout)
                        RemovePlayer(i);
                }

                if (messenger.Loggedin)
                {
                    if (IsJoined && taskOrder++ % 2 == 0)
                        Send(EventCode.Alive, false, sendBuffer);
                    else
                        SendPing();

                    if (connectionState != IsConnected)
                    {
                        connectionState = IsConnected;
                        if (connectionState == false)
                            OnError?.Invoke(Error.Disconnected);
                    }
                }
                else if (messenger.Started)
                    Login();

                yield return wait;
            }
        }

        private void Update()
        {
            var now = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ServerTime = now + deltaTime;

            messenger.Update(Time.unscaledDeltaTime);
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            var constructor = SynchronizationContext.Current.GetType().GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(int) }, null);
            var newContext = constructor.Invoke(new object[] { Thread.CurrentThread.ManagedThreadId });
            SynchronizationContext.SetSynchronizationContext(newContext as SynchronizationContext);
        }
#endif

        private void SendContinuousMyName(byte eventCode, sbyte otherId, int count)
        {
            StartCoroutine(ISendContinuousMyName(eventCode, otherId, count));
        }

        private IEnumerator ISendContinuousMyName(byte eventCode, sbyte otherId, int count)
        {
            var wait = new WaitForSecondsRealtime(0.2f);
            while (count-- > 0)
            {
                if (otherId >= 0)
                    Send(eventCode, false, new BufferWriter(128).AppendString(Player.Name), Target.Player, otherId);
                else
                    Send(eventCode, false, new BufferWriter(128).AppendString(Player.Name));
                yield return wait;
            }
        }


        //////////////////////////////////////////////////////
        /// STATIC MEMBERS
        //////////////////////////////////////////////////////
        private static Radio instance = null;
        private static Action onConnected = null;
        private static readonly byte[] cacheBytes = new byte[512];
        private static readonly BufferWriter sendBuffer = new BufferWriter(512);
        private static readonly NetPlayer myPlayer = new NetPlayer();
        private static readonly Messenger messenger = new Messenger();
        private static readonly NetPlayer[] players = new NetPlayer[maxPlayers];

        private static bool logingin = false;
        private static ulong deltaTime = 0;
        private static float aliveTime = -10;
        private static float DeathTime => Time.realtimeSinceStartup - aliveTime;

        public static event Action<Error> OnError = null;
        public static event Action OnMasterChanged = null;
        public static event Action<NetPlayer> OnPlayerConnected = null;
        public static event Action<NetPlayer> OnPlayerRemoved = null;
        public static event Action<NetPlayer, byte, byte[], byte> OnReceived = null;

        public static float ConnectionTimeout { get; set; } = 15;
        public static float PlayerActiveTimeout { get; set; } = 5;
        public static float PlayerDestoryTimeout { get; set; } = 30;
        public static byte PlayersCount { get; private set; } = 0;
        public static ulong Ping { get; private set; } = 0;
        public static bool IsMaster { get; private set; } = true;
        public static ulong ServerTime { get; private set; } = 0;

        public static NetPlayer Player => myPlayer;
        public static uint Token => messenger.Token;
        public static short RoomId => messenger.Room;
        public static sbyte PlayerId => messenger.Index;
        public static bool IsConnected => messenger.Loggedin && DeathTime < ConnectionTimeout;
        public static bool IsJoined => RoomId >= 0;


        public static float DebugDelayFactor
        {
            get => messenger.DelayFactor;
            set => messenger.DelayFactor = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            instance = new GameObject(nameof(Radio)).AddComponent<Radio>();
            DontDestroyOnLoad(instance);
        }

        public static void Connect(string serverAddress, byte[] deviceId, Action onConnectedToServer)
        {
            if (messenger.Started) return;
            onConnected = onConnectedToServer;
            var addressParts = serverAddress.Split(':');
            var serverIpPort = new IPEndPoint(IPAddress.Parse(addressParts[0]), int.Parse(addressParts[1]));
            messenger.Start(deviceId, serverIpPort, OnReceivedMessage);
        }

        public static void Disconnect(Action callback)
        {
            if (messenger.Stopped)
            {
                callback?.Invoke();
                return;
            }

            messenger.Logout(() =>
            {
                messenger.Stop();
                Ping = 0;

                for (int i = 0; i < maxPlayers; i++)
                    RemovePlayer(i);

                callback?.Invoke();
            });
        }

        private static void Login(Action callback = null)
        {
            if (messenger.Stopped || logingin) return;
            logingin = true;
            messenger.Login(error =>
            {
                logingin = false;
                if (ErrorExist(error) == false)
                {
                    aliveTime = Time.realtimeSinceStartup;
                    callback?.Invoke();
                    if (PlayerId >= 0)
                        AddPlayer(PlayerId, Player.Name);
                }
            });
        }

        public static void CreateRoom(string playerName, ushort openTimeout, byte[] properties, MatchmakingParams matchmaking, Action<short, sbyte> callback)
        {
            messenger.CreateRoom(openTimeout, properties, matchmaking, (error, roomId, playerId) =>
            {
                if (ErrorExist(error, () => CreateRoom(playerName, openTimeout, properties, matchmaking, callback))) return;
                callback?.Invoke(roomId, playerId);
                AddPlayer(playerId, playerName);
            });
        }

        public static void JoinRoom(string playerName, MatchmakingRanges matchmaking, Action<short, sbyte, byte[]> onJoined)
        {
            if (IsJoined) return;

            messenger.JoinRoom(matchmaking, (error, roomId, playerId, properties) =>
            {
                if (error == Error.NoError)
                {
                    onJoined?.Invoke(roomId, playerId, properties);
                    AddPlayer(playerId, playerName);
                    instance.SendContinuousMyName(EventCode.Checkin, -1, 20);
                }
                else if (error == Error.Expired)
                    ErrorExist(error, () => JoinRoom(playerName, matchmaking, onJoined));
                else
                    OnError?.Invoke(error);
            });
        }

        public static void LeaveRoom(Action callback)
        {
            if (RoomId < 0) return;

            messenger.LeaveRoom(error =>
            {
                if (ErrorExist(error)) return;
                callback?.Invoke();
            });
        }

        private static void SendPing()
        {
            messenger.SendPing((error, pingTime) =>
            {
                if (ErrorExist(error)) return;
                var now = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
                deltaTime = messenger.ServerTime - now;
                aliveTime = Time.realtimeSinceStartup;
                Ping = pingTime;

                var newMaster = messenger.Flag.HasFlag(Flag.Master);
                if (IsMaster != newMaster)
                {
                    IsMaster = newMaster;
                    OnMasterChanged?.Invoke();
                }
                onConnected?.Invoke();
                onConnected = null;
            });
        }

        private static bool ErrorExist(Error error, Action OnExpiredAndLoggedin = null)
        {
            if (error == Error.NoError) return false;

            if (error == Error.Expired)
                Login(OnExpiredAndLoggedin);
            else
                OnError?.Invoke(error);

            return true;
        }

        // send data to the other player. event code must be less that 200
        public static void Send(byte eventCode, bool reliable, BufferWriter data, Target target = Target.Other, sbyte otherId = -1)
        {
            if (IsConnected == false) return;

            sendBuffer.Reset();
            sendBuffer.AppendByte(eventCode);
            sendBuffer.AppendBytes(data.Bytes, data.Length);

            if (reliable)
                SendReliable(target, sendBuffer, otherId);
            else
                SendUnreliable(target, sendBuffer, otherId);
        }

        private static void SendUnreliable(Target target, BufferWriter data, sbyte otherId)
        {
            messenger.SendUnreliable(target, data, otherId);
        }

        private static void SendReliable(Target target, BufferWriter data, sbyte otherId)
        {
            switch (target)
            {
                case Target.All:
                    for (int i = 0; i < maxPlayers; i++)
                        if (players[i] != null)
                            messenger.SendReliable(players[i].Id, data);
                    break;
                case Target.Other:
                    for (int i = 0; i < maxPlayers; i++)
                        if (players[i] != null && players[i].IsOther)
                            messenger.SendReliable(players[i].Id, data);
                    break;
                case Target.Player:
                    messenger.SendReliable(otherId, data);
                    break;
            }
        }

        private static void OnReceivedMessage(Error error, sbyte senderId, BufferReader buffer, byte dataSize)
        {
            if (IsConnected == false || ErrorExist(error)) return;

            var player = players[senderId];
            var code = buffer.ReadByte();
            dataSize--;

            switch (code)
            {
                case EventCode.Alive:
                    if (player != null)
                    {
                        player.Stamptime();
                    }
                    break;

                case EventCode.Checkin:
                case EventCode.Welcome:
                    if (player == null)
                    {
                        var playerName = buffer.ReadString();
                        AddPlayer(senderId, playerName).Stamptime();
                        if (code == EventCode.Checkin)
                            instance.SendContinuousMyName(EventCode.Welcome, senderId, 20);
                    }
                    break;

                default:
                    if (player != null)
                    {
                        player.Stamptime();
                        buffer.ReadBytes(cacheBytes, dataSize);
                        OnReceived?.Invoke(player, code, cacheBytes, dataSize);
                    }
                    break;
            }
        }

        private static NetPlayer AddPlayer(sbyte id, string playerName)
        {
            var player = players[id];
            if (player != null) return player;
            PlayersCount++;

            player = players[id] = (id == PlayerId) ? myPlayer : new NetPlayer();
            player.SetId(id);
            player.SetName(playerName);
            OnPlayerConnected?.Invoke(player);
            return player;
        }

        private static void RemovePlayer(int index)
        {
            if (players[index] != null)
            {
                PlayersCount--;
                OnPlayerRemoved?.Invoke(players[index]);
            }
            players[index] = null;
        }
    }
}