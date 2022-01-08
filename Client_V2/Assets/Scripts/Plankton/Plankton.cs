using System;
using System.Collections.Generic;
using UnityEngine;
using SeganX.Network.Internal;
using System.Net;
using System.Collections;

#if UNITY_EDITOR
using System.Reflection;
using System.Threading;
#endif

namespace SeganX.Network
{
    public class Plankton : MonoBehaviour
    {
        public const int Capacity = 32;

        private IEnumerator Start()
        {
            var wait = new WaitForSecondsRealtime(1);
            while (true)
            {
                for (int i = 0; i < cache.Count; i++)
                {
                    var player = cache[i];
                    if (player == null || player.IsMine) continue;

                    var deltaTime = player.Update(PlayerActiveTimeout);
                    if (deltaTime > PlayerDestoryTimeout)
                    {
                        cache[i] = null;
                        players.Remove(player);
                        player.CallDestroy();
                        OnPlayerDestroyed?.Invoke(player);
                    }
                }

                if (messenger.Loggedin)
                    SendPing();

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
        private static Plankton instance = null;
        private static readonly Messenger messenger = new Messenger();
        private static readonly List<NetPlayer> cache = new List<NetPlayer>(Capacity);

        private static bool logingin = false;
        private static float aliveTime = -10;
        private static float DeathTime => Time.realtimeSinceStartup - aliveTime;


        public static readonly List<NetPlayer> players = new List<NetPlayer>(Capacity);
        public static event Action<Error> OnError = null;
        public static event Action<NetPlayer> OnPlayerConnected = null;
        public static event Action<NetPlayer> OnPlayerDestroyed = null;

        public static float PlayerActiveTimeout { get; set; } = 15;
        public static float PlayerDestoryTimeout { get; set; } = 300;
        public static long Ping { get; private set; } = 0;

        public static uint Token => messenger.Token;
        public static short RoomId => messenger.Room;
        public static sbyte PlayerId => messenger.Index;
        public static bool IsMaster => messenger.Flag.HasFlag(Flag.Master);
        public static long ServerTime => messenger.ServerTime;
        public static bool IsConnected => messenger.Loggedin && DeathTime < 5.0f;

        public static void Connect(string serverAddress, byte[] deviceId)
        {
            if (instance != null) return;

            for (int i = 0; i < cache.Capacity; i++)
                cache.Add(null);

            instance = new GameObject(nameof(Plankton)).AddComponent<Plankton>();
            //instance.gameObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(instance);

            var addressParts = serverAddress.Split(':');
            var serverIpPort = new IPEndPoint(IPAddress.Parse(addressParts[0]), int.Parse(addressParts[1]));
            messenger.Start(deviceId, serverIpPort, OnReceivedMessage);
            Login();
        }

        public static void Disconnect()
        {
            if (instance == null) return;

            Destroy(instance.gameObject);
            messenger.Stop();

            cache.Clear();
            foreach (var player in players)
            {
                player.CallDestroy();
                OnPlayerDestroyed?.Invoke(player);
            }
            players.Clear();
            Ping = 0;
        }

        private static void Login(Action OnSuccess = null)
        {
            if (instance == null || logingin) return;
            messenger.Login(error =>
            {
                logingin = false;
                if (ErrorExist(error) == false)
                    OnSuccess?.Invoke();
            });
        }

        public static void GetRooms(short startRoomIndex, byte count, bool excludeFullRooms, Action<byte, byte[]> callback)
        {
            if (instance == null) return;

            messenger.GetRooms(startRoomIndex, count, excludeFullRooms, (error, rcount, rooms) =>
            {
                if (ErrorExist(error, () => GetRooms(startRoomIndex, count, excludeFullRooms, callback))) return;
                callback?.Invoke(rcount, rooms);
            });
        }

        public static void JoinRoom(short roomIndex = -1, Action<short, sbyte> callback = null)
        {
            if (instance == null || RoomId >= 0) return;

            messenger.JoinRoom(roomIndex, (error, roomId, playerId) =>
            {
                if (ErrorExist(error, () => JoinRoom(roomIndex, callback))) return;
                callback?.Invoke(roomId, playerId);
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
            messenger.SendUnreliable(target, data, otherId);
        }

        public static void SendReliable(Target target, BufferWriter data, sbyte otherId = 0)
        {
            switch (target)
            {
                case Target.All:
                    foreach (var player in players)
                        messenger.SendReliable(player.Id, data);
                    break;
                case Target.Other:
                    foreach (var player in players)
                        if (player.IsOther)
                            messenger.SendReliable(player.Id, data);
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

        public static bool ErrorExist(Error error, Action OnExpiredAndLoggedin = null)
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
            if (ErrorExist(error)) return;

            var player = cache[senderId];
            if (player == null)
                player = AddPlayer(senderId);

            player.CallReceived(buffer, dataSize);
        }

        private static NetPlayer AddPlayer(sbyte id)
        {
            var player = cache[id];
            if (player != null) return player;

            player = cache[id] = new NetPlayer(id);
            players.Add(player);
            OnPlayerConnected?.Invoke(player);
            return player;
        }
    }
}