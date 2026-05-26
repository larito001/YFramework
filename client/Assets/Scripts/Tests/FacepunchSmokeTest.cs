using System.Collections.Generic;
using UnityEngine;
using YOTO.Gameplay.Net;
using YOTO.Network;

namespace YOTO.Tests
{
    /// <summary>
    /// 新网络架构的 smoke test。挂到 Boot 场景里 GameLoop 同一对象（或任意持久对象）即可。
    ///
    /// 直接消费 GameContext 里 bootstrap 好的服务，不再自己 Init SteamClient：
    ///   - SteamPlatform     : 本地玩家身份
    ///   - ILobbyService     : 建房/找房/进房/退房（fire-and-forget + 事件回调）
    ///   - INetworkSession   : StartServer / Connect / Subscribe&lt;T&gt; / Broadcast&lt;T&gt;
    ///
    /// 流程：
    ///   1. F1 切面板
    ///   2. Create / Find / Join lobby —— 走 ILobbyService
    ///   3. Lobby Joined 事件里，按 Host/Client 决定 session.StartServer() 或 session.Connect(info)
    ///   4. 输入框 + Send 按钮：Broadcast&lt;ChatMessage&gt; (protobuf)
    ///   5. Subscribe&lt;ChatMessage&gt; 收到的消息追加到 log
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacepunchSmokeTest : MonoBehaviour
    {
        [Tooltip("热键切换面板可见。")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [SerializeField] private int maxMembers = 4;

        private bool _visible = true;
        private readonly List<string> _log = new();
        private Vector2 _scrollLog, _scrollLobbies, _scrollMembers;

        private SteamPlatform _platform;
        private ILobbyService _lobby;
        private INetworkSession _session;

        private readonly List<LobbyInfo> _foundLobbies = new();
        private string _editJoinId = "";
        private string _chatDraft = "";

        // ────────────────────────────────────────────────
        //   Unity 生命周期
        // ────────────────────────────────────────────────

        private void Start()
        {
            // Awake 时 GameLoop 可能还没建好 Ctx；Start 之后所有 Awake 一定跑完。
            if (GameLoop.Instance == null || GameLoop.Instance.Ctx == null)
            {
                Log("GameLoop 未就绪，smoke test 退出。请把本组件挂在 Boot 场景。");
                enabled = false;
                return;
            }

            var ctx = GameLoop.Instance.Ctx;
            _platform = ctx.Get<SteamPlatform>();
            _lobby = ctx.Get<ILobbyService>();
            _session = ctx.Get<INetworkSession>();

            if (!_platform.IsValid)
            {
                Log("SteamPlatform 未初始化（IsValid=false），smoke test 停用。");
                enabled = false;
                return;
            }

            _lobby.Created += OnLobbyCreated;
            _lobby.Joined += OnLobbyJoined;
            _lobby.JoinFailed += OnLobbyJoinFailed;
            _lobby.Left += OnLobbyLeft;
            _lobby.MemberJoined += OnLobbyMemberJoined;
            _lobby.MemberLeft += OnLobbyMemberLeft;
            _lobby.InviteReceived += OnLobbyInvite;
            _lobby.Found += OnLobbyFound;
            _session.StateChanged += OnSessionStateChanged;

            _session.Subscribe<ChatMessage>(OnChatReceived);

            Log($"Smoke test ready. Me: {_platform.LocalName} ({_platform.LocalPeer})");
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
        }

        private void OnDestroy()
        {
            if (_lobby != null)
            {
                _lobby.Created -= OnLobbyCreated;
                _lobby.Joined -= OnLobbyJoined;
                _lobby.JoinFailed -= OnLobbyJoinFailed;
                _lobby.Left -= OnLobbyLeft;
                _lobby.MemberJoined -= OnLobbyMemberJoined;
                _lobby.MemberLeft -= OnLobbyMemberLeft;
                _lobby.InviteReceived -= OnLobbyInvite;
                _lobby.Found -= OnLobbyFound;
            }
            if (_session != null)
            {
                _session.StateChanged -= OnSessionStateChanged;
                _session.Unsubscribe<ChatMessage>();
            }
        }

        // ────────────────────────────────────────────────
        //   服务事件回调
        // ────────────────────────────────────────────────

        private void OnLobbyCreated(LobbyInfo info)
            => Log($"[Lobby] Created id={info.Id}");

        private void OnLobbyJoined(LobbyInfo info)
        {
            _foundLobbies.Clear();
            Log($"[Lobby] Joined id={info.Id} owner={info.Owner} members={info.MemberCount}");

            // Lobby ↔ Session 解耦：进房后由 gameplay 决定 session 拓扑。
            // 这里走最常见的 Lobby-Owner-as-Host 模式。
            if (info.Owner == _platform.LocalPeer)
            {
                _session.StartServer();
                Log("[Session] StartServer (I'm host)");
            }
            else
            {
                _session.Connect(info.ToSessionInfo());
                Log($"[Session] Connect → host {info.Owner}");
            }
        }

        private void OnLobbyJoinFailed(LobbyJoinFailure f)
            => Log($"[Lobby] Join FAILED id={f.LobbyId}: {f.Reason}");

        private void OnLobbyLeft(LobbyInfo info)
        {
            _session.Disconnect();
            Log($"[Lobby] Left id={info.Id}");
        }

        private void OnLobbyMemberJoined(LobbyInfo info, LobbyMember m)
            => Log($"[Lobby +] {m.DisplayName} ({m.Peer})");

        private void OnLobbyMemberLeft(LobbyInfo info, LobbyMember m)
            => Log($"[Lobby -] {m.DisplayName} ({m.Peer})");

        private void OnLobbyInvite(LobbyInfo info)
            => Log($"[Invite] -> {info.Id}");

        private void OnLobbyFound(IReadOnlyList<LobbyInfo> list)
        {
            _foundLobbies.Clear();
            _foundLobbies.AddRange(list);
            Log($"[Find] {list.Count} lobby(ies)");
        }

        private void OnSessionStateChanged()
            => Log($"[Session] state → {_session.State}");

        private void OnChatReceived(NetworkContext ctx, ChatMessage msg)
            => Log($"[Recv {msg.Sender}] {msg.Text}");

        // ────────────────────────────────────────────────
        //   动作
        // ────────────────────────────────────────────────

        private void DoCreate() => _lobby.Create(maxMembers);
        private void DoFind() => _lobby.Find();
        private void DoJoinById(ulong id) => _lobby.Join(id);
        private void DoLeave() => _lobby.Leave();

        private void SendChat()
        {
            if (string.IsNullOrEmpty(_chatDraft)) return;
            if (!_lobby.IsInLobby) { Log("[Chat] 没在 lobby 里，先 join."); return; }
            if (_session.State != ConnectionState.Connected)
            {
                Log($"[Chat] session 还没 Connected (state={_session.State})，等一下");
                return;
            }

            var msg = new ChatMessage
            {
                Sender = _platform.LocalName,
                Text = _chatDraft,
                Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            _session.Broadcast(msg);
            Log($"[Send] {msg.Sender}: {msg.Text}");
            _chatDraft = "";
        }

        // ────────────────────────────────────────────────
        //   IMGUI
        // ────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;
            const float w = 460f;
            float h = Mathf.Min(Screen.height - 20f, 760f);
            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);
            GUILayout.Label($"<b>Network Smoke Test</b>  (toggle: {toggleKey})");

            if (_platform == null || !_platform.IsValid)
            {
                GUILayout.Label("SteamPlatform NOT valid — 看日志。");
                DrawLog();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Me: {_platform.LocalName}  ({_platform.LocalPeer})");
            var cur = _lobby.Current;
            GUILayout.Label($"InLobby: {_lobby.IsInLobby}" +
                            (cur.HasValue ? $"  id={cur.Value.Id}  members={cur.Value.MemberCount}" : "") +
                            $"   Session: {_session.State}");

            GUILayout.Space(4);
            DrawRoomActions();
            DrawJoinByIdRow();

            if (!_lobby.IsInLobby && _foundLobbies.Count > 0) DrawFoundLobbies();
            if (_lobby.IsInLobby) DrawMembersAndChat();

            DrawLog();
            GUILayout.EndArea();
        }

        private void DrawRoomActions()
        {
            GUILayout.BeginHorizontal();
            try
            {
                GUI.enabled = !_lobby.IsInLobby;
                if (GUILayout.Button("Create")) DoCreate();
                if (GUILayout.Button("Find")) DoFind();
                GUI.enabled = _lobby.IsInLobby;
                if (GUILayout.Button("Leave")) DoLeave();
                if (GUILayout.Button("Invite Overlay"))
                {
                    if (_lobby.OpenInviteOverlay())
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
                GUI.enabled = !_lobby.IsInLobby && !string.IsNullOrEmpty(_editJoinId);
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
                GUILayout.Label($"{l.Id} [{l.MemberCount}/{l.MaxMembers}] tag={l.Tag}");
                if (GUILayout.Button("Join", GUILayout.Width(60))) DoJoinById(l.Id);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawMembersAndChat()
        {
            GUILayout.Space(4);
            GUILayout.Label($"<b>Members</b> ({_lobby.Members.Count})");
            _scrollMembers = GUILayout.BeginScrollView(_scrollMembers, GUILayout.Height(120));
            foreach (var m in _lobby.Members)
            {
                GUILayout.BeginHorizontal();
                string flag = m.Peer == _platform.LocalPeer ? "[me]" : "    ";
                GUILayout.Label($"{flag} {m.DisplayName} ({m.Peer})");
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4);
            GUILayout.Label("<b>Chat</b> (protobuf ChatMessage broadcast)");
            GUILayout.BeginHorizontal();
            try
            {
                _chatDraft = GUILayout.TextField(_chatDraft ?? "");
                GUI.enabled = !string.IsNullOrEmpty(_chatDraft) && _session.State == ConnectionState.Connected;
                if (GUILayout.Button("Send", GUILayout.Width(70))) SendChat();
            }
            finally { GUI.enabled = true; GUILayout.EndHorizontal(); }
        }

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
            Debug.Log("[NetSmoke] " + s);
        }
    }
}
