using System;
using UnityEngine;
using SeganX.Realtime.Internal;
using System.Net;
using System.Collections;

#if UNITY_EDITOR
using System.Reflection;
using System.Threading;
#endif

namespace SeganX.Realtime
{
    public class Radio : MonoBehaviour
    {
        private const int maxPlayers = 16;

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
                    SendPing();
                else if (messenger.Started)
                    Login();

                yield return wait;
            }
        }

        private void Update()
        {
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


        //////////////////////////////////////////////////////
        /// STATIC MEMBERS
        //////////////////////////////////////////////////////
        private static Radio instance = null;
        private static Action onConnected = null;
        private static readonly NetPlayer myPlayer = new NetPlayer();
        private static readonly Messenger messenger = new Messenger();
        private static readonly NetPlayer[] players = new NetPlayer[maxPlayers];

        private static bool logingin = false;
        private static float aliveTime = -10;
        private static float DeathTime => Time.realtimeSinceStartup - aliveTime;

        public static event Action<Error> OnError = null;
        public static event Action<NetPlayer> OnPlayerConnected = null;
        public static event Action<NetPlayer> OnPlayerDestroyed = null;

        public static float PlayerActiveTimeout { get; set; } = 5;
        public static float PlayerDestoryTimeout { get; set; } = 30;
        public static byte PlayersCount { get; private set; } = 0;
        public static long Ping { get; private set; } = 0;

        public static NetPlayer Player => myPlayer;
        public static uint Token => messenger.Token;
        public static short RoomId => messenger.Room;
        public static sbyte PlayerId => messenger.Index;
        public static bool IsMaster => messenger.Flag.HasFlag(Flag.Master);
        public static ulong ServerTime => messenger.ServerTime;
        public static bool IsConnected => messenger.Loggedin && DeathTime < 5.0f;

        public static void Connect(string serverAddress, byte[] deviceId, Action callback)
        {
            if (instance != null) return;
            onConnected = callback;

            instance = new GameObject(nameof(Radio)).AddComponent<Radio>();
            DontDestroyOnLoad(instance);

            var addressParts = serverAddress.Split(':');
            var serverIpPort = new IPEndPoint(IPAddress.Parse(addressParts[0]), int.Parse(addressParts[1]));
            messenger.Start(deviceId, serverIpPort, OnReceivedMessage);
        }

        public static void Disconnect(Action callback)
        {
            if (instance == null) return;

            messenger.Logout(() =>
            {
                Destroy(instance.gameObject);
                messenger.Stop();
                Ping = 0;

                for (int i = 0; i < maxPlayers; i++)
                    RemovePlayer(i);

                callback?.Invoke();
            });
        }

        private static void Login(Action callback = null)
        {
            if (instance == null || logingin) return;
            logingin = true;
            messenger.Login(error =>
            {
                logingin = false;
                if (ErrorExist(error) == false)
                {
                    aliveTime = Time.realtimeSinceStartup;
                    callback?.Invoke();
                    onConnected?.Invoke();
                    onConnected = null;
                    if (PlayerId >= 0)
                        AddPlayer(PlayerId);
                }
            });
        }

        public static void CreateRoom(short openTimeout, byte[] properties, MatchmakingParams matchmaking, Action<short, sbyte> callback)
        {
            if (instance == null) return;

            messenger.CreateRoom(openTimeout, properties, matchmaking, (error, roomId, playerId) =>
            {
                if (ErrorExist(error, () => CreateRoom(openTimeout, properties, matchmaking, callback))) return;
                callback?.Invoke(roomId, playerId);
                AddPlayer(playerId);
            });
        }

        public static void JoinRoom(MatchmakingRanges matchmaking, Action<short, sbyte, byte[]> callback = null)
        {
            if (instance == null || RoomId >= 0) return;

            messenger.JoinRoom(matchmaking, (error, roomId, playerId, properties) =>
            {
                if (ErrorExist(error, () => JoinRoom(matchmaking, callback))) return;
                callback?.Invoke(roomId, playerId, properties);
                AddPlayer(playerId);
            });
        }

        public static void LeaveRoom(Action callback)
        {
            if (instance == null || RoomId < 0) return;

            messenger.LeaveRoom(error =>
            {
                if (ErrorExist(error)) return;
                callback?.Invoke();
            });
        }

        public static void SendUnreliable(Target target, BufferWriter data, sbyte otherId = 0)
        {
            if (IsConnected)
                messenger.SendUnreliable(target, data, otherId);
        }

        public static void SendReliable(Target target, BufferWriter data, sbyte otherId = 0)
        {
            if (IsConnected == false) return;

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

        private static void SendPing()
        {
            if (instance == null) return;

            messenger.SendPing((error, pingTime) =>
            {
                if (ErrorExist(error)) return;
                aliveTime = Time.realtimeSinceStartup;
                Ping = pingTime;
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

        private static void OnReceivedMessage(Error error, sbyte senderId, BufferReader buffer, byte dataSize)
        {
            if (IsConnected == false || ErrorExist(error)) return;

            var player = players[senderId];
            if (player == null)
                player = AddPlayer(senderId);

            player.CallReceived(buffer, dataSize);
        }

        private static NetPlayer AddPlayer(sbyte id)
        {
            var player = players[id];
            if (player != null) return player;
            PlayersCount++;

            player = players[id] = (id == PlayerId) ? myPlayer : new NetPlayer();
            player.SetId(id);
            OnPlayerConnected?.Invoke(player);
            return player;
        }

        private static void RemovePlayer(int index)
        {
            if (players[index] != null)
            {
                PlayersCount--;
                OnPlayerDestroyed?.Invoke(players[index]);
            }
            players[index] = null;
        }
    }
}