#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace YOTO.Net
{
    /// <summary>
    /// IMGUI 调试面板。挂到任意 GameObject（推荐挂在 GameLoop 上）即可。
    /// 启动后顶 GameLoop 的初始化跑完，从 GameLoop.Instance.Ctx 拿 NetManager。
    /// </summary>
    public sealed class NetTestPanel : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private int maxMembers = 4;

        private NetManager _net;
        private bool _visible = true;
        private readonly List<string> _log = new();
        private Vector2 _scrollLog;
        private Vector2 _scrollLobbies;

#if !DISABLESTEAMWORKS
        private readonly List<CSteamID> _foundLobbies = new();
        private readonly List<CSteamID> _peers = new();
#endif

        private void Start()
        {
            if (GameLoop.Instance == null || GameLoop.Instance.Ctx == null)
            {
                Log("GameLoop not ready, retry next frame...");
                return;
            }
            Bind();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
            if (_net == null && GameLoop.Instance?.Ctx != null) Bind();
        }

        private void OnDestroy() => Unbind();

        private void Bind()
        {
            if (_net != null) return;
            if (!GameLoop.Instance.Ctx.TryGet<NetManager>(out _net))
            {
                Log("NetManager not registered.");
                return;
            }
#if !DISABLESTEAMWORKS
            _net.RoomJoined += OnRoomJoined;
            _net.RoomLeft += OnRoomLeft;
            _net.RoomFailed += OnRoomFailed;
            _net.PlayerJoined += OnPlayerJoined;
            _net.PlayerLeft += OnPlayerLeft;
            _net.MessageReceived += OnMessage;
            _net.Lobby.Found += OnLobbiesFound;
#endif
            Log("NetTestPanel bound. Steam initialized = " + SteamManager.Initialized);
        }

        private void Unbind()
        {
            if (_net == null) return;
#if !DISABLESTEAMWORKS
            _net.RoomJoined -= OnRoomJoined;
            _net.RoomLeft -= OnRoomLeft;
            _net.RoomFailed -= OnRoomFailed;
            _net.PlayerJoined -= OnPlayerJoined;
            _net.PlayerLeft -= OnPlayerLeft;
            _net.MessageReceived -= OnMessage;
            _net.Lobby.Found -= OnLobbiesFound;
#endif
        }

#if !DISABLESTEAMWORKS
        private void OnRoomJoined() => Log($"[Room] joined. host={_net.IsHost} lobby={(ulong)_net.Lobby.CurrentLobby}");
        private void OnRoomLeft() { _peers.Clear(); Log("[Room] left."); }
        private void OnRoomFailed(EResult r) => Log($"[Room] FAILED: {r}");

        private void OnPlayerJoined(CSteamID id)
        {
            if (!_peers.Contains(id)) _peers.Add(id);
            Log($"[Peer +] {Name(id)}");
        }

        private void OnPlayerLeft(CSteamID id)
        {
            _peers.Remove(id);
            Log($"[Peer -] {Name(id)}");
        }

        private void OnMessage(CSteamID from, ArraySegment<byte> data)
        {
            var text = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            Log($"[Recv] {Name(from)}: {text}");
        }

        private void OnLobbiesFound(CSteamID[] lobbies)
        {
            _foundLobbies.Clear();
            _foundLobbies.AddRange(lobbies);
            Log($"[Find] {lobbies.Length} lobby(ies)");
        }

        private static string Name(CSteamID id)
        {
            var n = SteamFriends.GetFriendPersonaName(id);
            return string.IsNullOrEmpty(n) ? id.ToString() : $"{n}({(ulong)id})";
        }
#endif

        private void OnGUI()
        {
            if (!_visible) return;
            const float w = 380f;
            float h = Mathf.Min(Screen.height - 20f, 600f);
            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);
            GUILayout.Label($"<b>NetTestPanel</b>  (toggle: {toggleKey})");

#if DISABLESTEAMWORKS
            GUILayout.Label("Steamworks disabled on this build.");
#else
            if (_net == null) { GUILayout.Label("NetManager not ready."); GUILayout.EndArea(); return; }

            GUILayout.Label($"Steam: {(SteamManager.Initialized ? "OK" : "NOT INIT")}   " +
                            $"InRoom: {_net.InRoom}   Host: {_net.IsHost}");
            if (_net.InRoom)
            {
                GUILayout.Label($"Lobby: {(ulong)_net.Lobby.CurrentLobby}");
                GUILayout.Label($"Owner: {Name(_net.Lobby.Owner)}");
                GUILayout.Label($"Connected peers: {_peers.Count}");
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = !_net.InRoom && SteamManager.Initialized;
            if (GUILayout.Button($"Create ({maxMembers})")) _net.CreateRoom(maxMembers);
            if (GUILayout.Button("Find")) _net.Lobby.FindLobby();
            GUI.enabled = !_net.InRoom && _foundLobbies.Count > 0;
            if (GUILayout.Button("Join First")) _net.JoinRoom(_foundLobbies[0]);
            GUI.enabled = _net.InRoom;
            if (GUILayout.Button("Leave")) _net.LeaveRoom();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _net.InRoom && _peers.Count > 0;
            if (GUILayout.Button("Send Ping (reliable)")) SendPing(true);
            if (GUILayout.Button("Send Ping (unreliable)")) SendPing(false);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_foundLobbies.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label($"<b>Found lobbies</b> ({_foundLobbies.Count})");
                _scrollLobbies = GUILayout.BeginScrollView(_scrollLobbies, GUILayout.Height(120));
                foreach (var lid in _foundLobbies)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(((ulong)lid).ToString());
                    GUI.enabled = !_net.InRoom;
                    if (GUILayout.Button("Join", GUILayout.Width(60))) _net.JoinRoom(lid);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
#endif

            GUILayout.Space(6);
            GUILayout.Label("<b>Log</b>");
            _scrollLog = GUILayout.BeginScrollView(_scrollLog, GUILayout.ExpandHeight(true));
            for (int i = _log.Count - 1; i >= 0; i--) GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

#if !DISABLESTEAMWORKS
        private int _pingSeq;

        private void SendPing(bool reliable)
        {
            var msg = $"ping#{++_pingSeq} from {Name(SteamUser.GetSteamID())} t={Time.time:F2}";
            var bytes = Encoding.UTF8.GetBytes(msg);
            foreach (var p in _peers)
                _net.Send(p, bytes, 0, bytes.Length, reliable);
            Log($"[Send] -> {_peers.Count} peer(s): {msg}");
        }
#endif

        private void Log(string s)
        {
            var line = $"{Time.time:F2}  {s}";
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
            Debug.Log("[NetTest] " + s);
        }
    }
}
