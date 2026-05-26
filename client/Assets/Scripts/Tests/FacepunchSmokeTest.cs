#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
using Steamworks.Data;
#endif

namespace YOTO.Tests
{
    /// <summary>
    /// Facepunch.Steamworks 装好后的 smoke test。挂到任意 GameObject 上即可。
    /// 验证项：
    ///   - SteamClient.Init / RunCallbacks / Shutdown
    ///   - 本地玩家信息（Name + SteamID）
    ///   - Lobby: Create / Find / Join (by id / by list) / Leave / Invite Overlay
    ///   - P2P：定向 Send (R/U)、Broadcast、收包日志
    /// 走的是老 SteamNetworking P2P API（最简单的验证路径，relay 自动接管）。
    /// F1 切换面板可见。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacepunchSmokeTest : MonoBehaviour
    {
        [Tooltip("Steam AppID。480 是 Valve Spacewar 测试 AppID，配合项目根目录的 steam_appid.txt 使用。")]
        [SerializeField] private uint appId = 480;

        [Tooltip("热键切换面板可见。")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Find/Create 时写入 lobby data 的标签，便于和其他玩家区分。空字符串则不过滤。")]
        [SerializeField] private string gameTag = "yfw";

        [SerializeField] private int maxMembers = 4;

        private bool _visible = true;
        private readonly List<string> _log = new();
        private Vector2 _scrollLog, _scrollLobbies, _scrollMembers;

#if !DISABLESTEAMWORKS
        private bool _initialized;
        private Lobby? _currentLobby;
        private readonly List<Lobby> _foundLobbies = new();
        // 已经 Accept 过 P2P session 的对端，离场时关掉
        private readonly HashSet<SteamId> _knownPeers = new();
        private string _editJoinId = "";
        private int _pingSeq;

        // ════════════════════════════════════════════════
        //   Unity 生命周期
        // ════════════════════════════════════════════════

        private void Awake()
        {
            // 失焦也跑 Update，否则 P2P 包堆积、Lobby 回调延迟
            Application.runInBackground = true;

            // 域重载后 SteamClient 可能仍是 Valid（Editor PlayMode 多次进出），避免重复 Init 抛异常
            if (SteamClient.IsValid)
            {
                _initialized = true;
                Log("SteamClient 已经初始化（复用上次 PlayMode 残留）");
            }
            else
            {
                try
                {
                    // asyncCallbacks=false：自己每帧驱动 RunCallbacks，可控、便于排错
                    SteamClient.Init(appId, asyncCallbacks: false);
                    _initialized = SteamClient.IsValid;
                    if (_initialized)
                        Log($"Init OK  appId={appId}  name={SteamClient.Name}  id={SteamClient.SteamId}");
                    else
                        Log("Init 返回但 IsValid=false");
                }
                catch (System.DllNotFoundException e)
                {
                    Log("DllNotFound：确认 Assets/Plugins/Facepunch.Steamworks/ 下 dll 齐全；Editor 也要重启一次让 Unity 重新加载 native 库。\n" + e.Message);
                }
                catch (System.Exception e)
                {
                    Log("Init 异常：" + e);
                }
            }

            if (_initialized) Subscribe();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
            if (!_initialized) return;

            try { SteamClient.RunCallbacks(); }
            catch (System.Exception e) { Log("RunCallbacks 异常：" + e.Message); }

            // poll P2P 包（老 API；批量取空再退出）
            while (SteamNetworking.IsP2PPacketAvailable(channel: 0))
            {
                var pkt = SteamNetworking.ReadP2PPacket(channel: 0);
                if (!pkt.HasValue) break;
                var text = Encoding.UTF8.GetString(pkt.Value.Data);
                Log($"[Recv {NameOf(pkt.Value.SteamId)}] {text}");
            }
        }

        private void OnApplicationQuit()
        {
            Teardown();
        }

        private void OnDestroy()
        {
            // OnApplicationQuit 在 Editor 停止 PlayMode 时也会触发；OnDestroy 兜底（脚本被禁用 / 对象销毁）
            Teardown();
        }

        private void Teardown()
        {
            if (!_initialized) return;
            if (_currentLobby.HasValue)
            {
                foreach (var p in _knownPeers) SteamNetworking.CloseP2PSessionWithUser(p);
                _knownPeers.Clear();
                _currentLobby.Value.Leave();
                _currentLobby = null;
            }
            Unsubscribe();
            SteamClient.Shutdown();
            _initialized = false;
        }

        // ════════════════════════════════════════════════
        //   订阅 / 反订阅 Facepunch 事件
        // ════════════════════════════════════════════════

        private void Subscribe()
        {
            SteamMatchmaking.OnLobbyCreated     += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered     += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite      += OnLobbyInvite;
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
            SteamNetworking.OnP2PConnectionFailed += OnP2PConnectionFailed;
        }

        private void Unsubscribe()
        {
            SteamMatchmaking.OnLobbyCreated     -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered     -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite      -= OnLobbyInvite;
            SteamNetworking.OnP2PSessionRequest -= OnP2PSessionRequest;
            SteamNetworking.OnP2PConnectionFailed -= OnP2PConnectionFailed;
        }

        // ════════════════════════════════════════════════
        //   事件回调
        // ════════════════════════════════════════════════

        private void OnLobbyCreated(Result r, Lobby lobby)
        {
            if (r != Result.OK) { Log($"[Lobby] Create FAILED: {r}"); return; }
            lobby.SetPublic();
            lobby.SetJoinable(true);
            if (!string.IsNullOrEmpty(gameTag)) lobby.SetData("game", gameTag);
            Log($"[Lobby] Created id={lobby.Id}");
            // 紧接着 Facepunch 会派 OnLobbyEntered，状态写在那里
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            _currentLobby = lobby;
            _foundLobbies.Clear();
            Log($"[Lobby] Entered id={lobby.Id} owner={NameOf(lobby.Owner.Id)} members={lobby.MemberCount}");
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            Log($"[Lobby +] {friend.Name}({friend.Id})");
            // 主动捅一个握手包：触发对端 OnP2PSessionRequest，链路就立起来了
            if (friend.Id != SteamClient.SteamId)
                SendTo(friend.Id, $"hello from {SteamClient.Name}", reliable: true);
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            Log($"[Lobby -] {friend.Name}({friend.Id})");
            if (_knownPeers.Remove(friend.Id))
                SteamNetworking.CloseP2PSessionWithUser(friend.Id);
        }

        private void OnLobbyInvite(Friend inviter, Lobby lobby) =>
            Log($"[Invite] from {inviter.Name} -> {lobby.Id}");

        private void OnP2PSessionRequest(SteamId requester)
        {
            // 不 Accept 就永远收不到包。生产应再校验 requester 是否在当前 lobby 成员里。
            SteamNetworking.AcceptP2PSessionWithUser(requester);
            _knownPeers.Add(requester);
            Log($"[P2P] Accept session from {NameOf(requester)}");
        }

        private void OnP2PConnectionFailed(SteamId who, P2PSessionError err) =>
            Log($"[P2P] FAIL {NameOf(who)}: {err}");

        // ════════════════════════════════════════════════
        //   操作
        // ════════════════════════════════════════════════

        private async void DoCreate()
        {
            var r = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            if (!r.HasValue) Log("[Lobby] CreateLobbyAsync returned null");
            // 成功路径走 OnLobbyCreated + OnLobbyEntered
        }

        private async void DoFind()
        {
            var builder = SteamMatchmaking.LobbyList.WithMaxResults(50);
            if (!string.IsNullOrEmpty(gameTag)) builder = builder.WithKeyValue("game", gameTag);
            var lobbies = await builder.RequestAsync();
            _foundLobbies.Clear();
            if (lobbies != null) _foundLobbies.AddRange(lobbies);
            Log($"[Find] {_foundLobbies.Count} lobby(ies)");
        }

        private async void DoJoin(Lobby lobby)
        {
            var r = await lobby.Join();
            if (r != RoomEnter.Success) Log($"[Lobby] Join FAILED: {r}");
        }

        private async void DoJoinById(ulong raw)
        {
            var lobby = new Lobby(raw);
            var r = await lobby.Join();
            if (r != RoomEnter.Success) Log($"[Lobby] JoinById FAILED: {r}");
        }

        private void DoLeave()
        {
            if (!_currentLobby.HasValue) return;
            foreach (var p in _knownPeers) SteamNetworking.CloseP2PSessionWithUser(p);
            _knownPeers.Clear();
            _currentLobby.Value.Leave();
            _currentLobby = null;
            Log("[Lobby] Left");
        }

        private void SendTo(SteamId target, string text, bool reliable)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var t = reliable ? P2PSend.Reliable : P2PSend.Unreliable;
            bool ok = SteamNetworking.SendP2PPacket(target, bytes, bytes.Length, 0, t);
            Log($"[Send->{NameOf(target)} {(ok ? "OK" : "FAIL")}] {text}");
        }

        private void Broadcast(bool reliable)
        {
            if (!_currentLobby.HasValue) return;
            var msg = $"bcast#{++_pingSeq} t={Time.time:F2}";
            var bytes = Encoding.UTF8.GetBytes(msg);
            var t = reliable ? P2PSend.Reliable : P2PSend.Unreliable;
            int sent = 0;
            foreach (var m in _currentLobby.Value.Members)
            {
                if (m.Id == SteamClient.SteamId) continue;
                if (SteamNetworking.SendP2PPacket(m.Id, bytes, bytes.Length, 0, t)) sent++;
            }
            Log($"[Send x{sent}] {msg}");
        }

        // ════════════════════════════════════════════════
        //   IMGUI
        // ════════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_visible) return;
            const float w = 440f;
            float h = Mathf.Min(Screen.height - 20f, 720f);
            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);
            GUILayout.Label($"<b>Facepunch Smoke Test</b>  (toggle: {toggleKey})");

            if (!_initialized)
            {
                GUILayout.Label("SteamClient NOT initialized — 看日志。");
                DrawLog();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Me: {SteamClient.Name}  ({SteamClient.SteamId})");
            GUILayout.Label($"InLobby: {_currentLobby.HasValue}" +
                            (_currentLobby.HasValue ? $"  id={_currentLobby.Value.Id}  members={_currentLobby.Value.MemberCount}" : ""));

            GUILayout.Space(4);
            DrawRoomActions();
            DrawJoinByIdRow();

            if (!_currentLobby.HasValue && _foundLobbies.Count > 0) DrawFoundLobbies();
            if (_currentLobby.HasValue) DrawMembersAndSend();

            DrawLog();
            GUILayout.EndArea();
        }

        private void DrawRoomActions()
        {
            GUILayout.BeginHorizontal();
            try
            {
                GUI.enabled = !_currentLobby.HasValue;
                if (GUILayout.Button("Create")) DoCreate();
                if (GUILayout.Button("Find")) DoFind();
                GUI.enabled = _currentLobby.HasValue;
                if (GUILayout.Button("Leave")) DoLeave();
                if (GUILayout.Button("Invite Overlay"))
                {
                    // Editor / 直接双击 exe 时 overlay 可能不弹，把 id 拷到剪贴板兜底
                    SteamFriends.OpenGameInviteOverlay(_currentLobby.Value.Id);
                    GUIUtility.systemCopyBuffer = _currentLobby.Value.Id.Value.ToString();
                    Log("[Invite] overlay called; id copied to clipboard");
                }
            }
            finally { GUI.enabled = true; GUILayout.EndHorizontal(); }
        }

        private void DrawJoinByIdRow()
        {
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Join by ID", GUILayout.Width(80));
                _editJoinId = GUILayout.TextField(_editJoinId ?? "");
                GUI.enabled = !_currentLobby.HasValue && !string.IsNullOrEmpty(_editJoinId);
                if (GUILayout.Button("Join", GUILayout.Width(60)))
                {
                    if (ulong.TryParse(_editJoinId.Trim(), out var raw)) DoJoinById(raw);
                    else Log("invalid lobby id");
                }
            }
            finally { GUI.enabled = true; GUILayout.EndHorizontal(); }
        }

        private void DrawFoundLobbies()
        {
            GUILayout.Space(4);
            GUILayout.Label($"<b>Found</b> ({_foundLobbies.Count})");
            _scrollLobbies = GUILayout.BeginScrollView(_scrollLobbies, GUILayout.Height(110));
            foreach (var l in _foundLobbies)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{l.Id} [{l.MemberCount}/{l.MaxMembers}] tag={l.GetData("game")}");
                if (GUILayout.Button("Join", GUILayout.Width(60))) DoJoin(l);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawMembersAndSend()
        {
            GUILayout.Space(4);
            GUILayout.Label($"<b>Members</b> ({_currentLobby.Value.MemberCount})");
            _scrollMembers = GUILayout.BeginScrollView(_scrollMembers, GUILayout.Height(140));
            foreach (var m in _currentLobby.Value.Members)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    string flag = m.Id == SteamClient.SteamId ? "[me]" : "    ";
                    GUILayout.Label($"{flag} {m.Name} ({m.Id})");
                    GUI.enabled = m.Id != SteamClient.SteamId;
                    if (GUILayout.Button("R", GUILayout.Width(28))) SendTo(m.Id, $"direct#{++_pingSeq} R", true);
                    if (GUILayout.Button("U", GUILayout.Width(28))) SendTo(m.Id, $"direct#{++_pingSeq} U", false);
                }
                finally { GUI.enabled = true; GUILayout.EndHorizontal(); }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bcast R")) Broadcast(true);
            if (GUILayout.Button("Bcast U")) Broadcast(false);
            GUILayout.EndHorizontal();
        }

        // ════════════════════════════════════════════════
        //   小工具
        // ════════════════════════════════════════════════

        private static string NameOf(SteamId id)
        {
            var n = new Friend(id).Name;
            return string.IsNullOrEmpty(n) ? id.Value.ToString() : $"{n}({id.Value})";
        }
#else
        private void OnGUI()
        {
            if (!_visible) return;
            GUILayout.BeginArea(new Rect(10, 10, 420, 80), GUI.skin.box);
            GUILayout.Label("<b>Facepunch Smoke Test</b>");
            GUILayout.Label("当前平台禁用 Steamworks。");
            GUILayout.EndArea();
        }
#endif

        private void DrawLog()
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>Log</b>");
            _scrollLog = GUILayout.BeginScrollView(_scrollLog, GUILayout.ExpandHeight(true));
            for (int i = _log.Count - 1; i >= 0; i--) GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();
        }

        private void Log(string s)
        {
            var line = $"{Time.realtimeSinceStartup:F2}  {s}";
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
            Debug.Log("[FpSmoke] " + s);
        }
    }
}
