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
    /// 网络调试面板。挂到任意 GameObject（推荐挂在 GameLoop 所在 GameObject 上）。
    ///
    /// 功能矩阵：
    ///   - Lobby：Create / Find / Join (by index or by id) / Leave / Invite
    ///   - Lobby 配置：Type（4 种）/ GameTag / MaxMembers
    ///   - Transport：广播 / 定向发送（reliable & unreliable）/ 接收日志
    ///   - 状态展示：大厅成员表，[me]/[T 已 transport 连通]/[ ] 三态标识
    ///
    /// 事件分两条线：
    ///   - Lobby +/-  ：Steam 大厅成员变化，仅表示对端已经进入 lobby（房号里有人）
    ///   - Transport +/- ：底层 P2P 握手完成或断开，是真正能 Send / Recv 的时刻
    /// 两者中间可能有几百毫秒到几秒的间隔，面板把两条线分开打日志便于排错。
    /// </summary>
    public sealed class NetTestPanel : MonoBehaviour
    {
        // ───────── Inspector ─────────

        [Tooltip("热键：显示/隐藏面板")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        // ───────── 框架引用 ─────────

        private NetManager _net;

        // ───────── UI 状态 ─────────

        private bool _visible = true;
        private readonly List<string> _log = new();
        private Vector2 _scrollLog;
        private Vector2 _scrollLobbies;
        private Vector2 _scrollMembers;

#if !DISABLESTEAMWORKS
        // ───────── 运行时状态 ─────────

        /// <summary>FindLobby 最新一批结果，按 OnLobbiesFound 写入。</summary>
        private readonly List<CSteamID> _foundLobbies = new();

        /// <summary>已经完成 P2P 握手、可以 Send 的 peer 集合。和大厅成员表不完全等价。</summary>
        private readonly HashSet<CSteamID> _connectedPeers = new();

        /// <summary>本地 Ping 序号，仅用于日志方便定位。</summary>
        private int _pingSeq;

        // ───────── 编辑态（Create / Join 表单值）─────────

        private string _editTag = "yfw";
        private string _editMaxMembers = "4";
        private string _editJoinId = "";
        private int _typeIdx = 2; // 默认 Public（最容易被 Find 看到）

        /// <summary>SelectionGrid 索引到 ELobbyType 的映射，顺序必须和 <see cref="LobbyTypeNames"/> 对齐。</summary>
        private static readonly ELobbyType[] LobbyTypes =
        {
            ELobbyType.k_ELobbyTypePrivate,
            ELobbyType.k_ELobbyTypeFriendsOnly,
            ELobbyType.k_ELobbyTypePublic,
            ELobbyType.k_ELobbyTypeInvisible,
        };
        private static readonly string[] LobbyTypeNames = { "Private", "Friends", "Public", "Invisible" };
#endif

        // ═════════════════════════════════════════════════════════════════
        //   Unity 生命周期
        // ═════════════════════════════════════════════════════════════════

        private void Start()
        {
            // GameLoop.Awake 里同步建好了 Ctx，理论上这里一定能拿到；保留判空兼容“先于 GameLoop 启动”的情形
            if (GameLoop.Instance == null || GameLoop.Instance.Ctx == null) return;
            Bind();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;

            // 兜底：Start 时 Ctx 还没好就晚一帧再绑
            if (_net == null && GameLoop.Instance?.Ctx != null) Bind();
        }

        private void OnDestroy() => Unbind();

        // ═════════════════════════════════════════════════════════════════
        //   绑定 / 解绑 NetManager 事件
        // ═════════════════════════════════════════════════════════════════

        private void Bind()
        {
            if (_net != null) return;
            if (!GameLoop.Instance.Ctx.TryGet<NetManager>(out _net))
            {
                Log("NetManager not registered.");
                return;
            }
#if !DISABLESTEAMWORKS
            // Room 级事件（NetManager 聚合）
            _net.RoomJoined   += OnRoomJoined;
            _net.RoomLeft     += OnRoomLeft;
            _net.RoomFailed   += OnRoomFailed;

            // Transport 级事件（P2P 握手完成 / 断开 / 收到字节）
            _net.PlayerJoined     += OnPlayerJoined;
            _net.PlayerLeft       += OnPlayerLeft;
            _net.MessageReceived  += OnMessage;

            // Lobby 级事件（Steam 大厅，可能早于 transport 就绪）
            _net.Lobby.Found        += OnLobbiesFound;
            _net.Lobby.MemberJoined += OnLobbyMemberJoined;
            _net.Lobby.MemberLeft   += OnLobbyMemberLeft;

            // 把 NetManager 的当前 GameTag 同步到面板的编辑框
            _editTag = _net.Lobby.GameTag;
#endif
            Log("NetTestPanel bound. Steam = " + SteamManager.Initialized);
        }

        private void Unbind()
        {
            if (_net == null) return;
#if !DISABLESTEAMWORKS
            _net.RoomJoined   -= OnRoomJoined;
            _net.RoomLeft     -= OnRoomLeft;
            _net.RoomFailed   -= OnRoomFailed;
            _net.PlayerJoined     -= OnPlayerJoined;
            _net.PlayerLeft       -= OnPlayerLeft;
            _net.MessageReceived  -= OnMessage;
            _net.Lobby.Found        -= OnLobbiesFound;
            _net.Lobby.MemberJoined -= OnLobbyMemberJoined;
            _net.Lobby.MemberLeft   -= OnLobbyMemberLeft;
#endif
        }

#if !DISABLESTEAMWORKS
        // ═════════════════════════════════════════════════════════════════
        //   事件回调
        // ═════════════════════════════════════════════════════════════════

        private void OnRoomJoined() => Log($"[Room] joined. host={_net.IsHost} lobby={(ulong)_net.Lobby.CurrentLobby}");

        private void OnRoomLeft()
        {
            // LeaveRoom 走的是“先 Transport.Close 再清表”，PlayerLeft 不会逐个上抛；这里统一清掉
            _connectedPeers.Clear();
            Log("[Room] left.");
        }

        private void OnRoomFailed(EResult r) => Log($"[Room] FAILED: {r}");

        private void OnPlayerJoined(CSteamID id)
        {
            _connectedPeers.Add(id);
            Log($"[Transport +] {Name(id)}");
        }

        private void OnPlayerLeft(CSteamID id)
        {
            _connectedPeers.Remove(id);
            Log($"[Transport -] {Name(id)}");
        }

        private void OnMessage(CSteamID from, ArraySegment<byte> data)
        {
            // Payload 仅在回调期间有效，转字符串就拷贝出来了，后面用没问题
            var text = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            Log($"[Recv] {Name(from)}: {text}");
        }

        private void OnLobbiesFound(CSteamID[] lobbies)
        {
            _foundLobbies.Clear();
            _foundLobbies.AddRange(lobbies);
            Log($"[Find] {lobbies.Length} lobby(ies)");
        }

        private void OnLobbyMemberJoined(CSteamID id) => Log($"[Lobby +] {Name(id)}");
        private void OnLobbyMemberLeft(CSteamID id) => Log($"[Lobby -] {Name(id)}");
#endif

        // ═════════════════════════════════════════════════════════════════
        //   OnGUI 入口（IMGUI 即时模式，每帧多次调用）
        // ═════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_visible) return;

            const float w = 420f;
            float h = Mathf.Min(Screen.height - 20f, 720f);   // 屏幕小就跟着缩，避免溢出
            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);
            GUILayout.Label($"<b>NetTestPanel</b>  (toggle: {toggleKey})");

#if DISABLESTEAMWORKS
            GUILayout.Label("Steamworks disabled on this build.");
#else
            if (_net == null)
            {
                GUILayout.Label("NetManager not ready.");
                GUILayout.EndArea();
                return;
            }

            DrawStatus();
            GUILayout.Space(4);

            // 入房前显示创建/加入表单；入房后显示成员区头
            if (_net.InRoom) DrawInRoomHeader();
            else DrawCreateConfig();

            GUILayout.Space(4);
            DrawActions();

            // Find 结果列表仅在“未入房 + 有结果”时显示
            if (!_net.InRoom && _foundLobbies.Count > 0)
            {
                GUILayout.Space(4);
                DrawFoundLobbies();
            }

            // 大厅成员列表仅在房间内显示
            if (_net.InRoom)
            {
                GUILayout.Space(4);
                DrawMembers();
            }
#endif

            // Log 占据所有剩余高度
            GUILayout.Space(6);
            GUILayout.Label("<b>Log</b>");
            _scrollLog = GUILayout.BeginScrollView(_scrollLog, GUILayout.ExpandHeight(true));
            for (int i = _log.Count - 1; i >= 0; i--) GUILayout.Label(_log[i]);  // 倒序：最新在最上
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

#if !DISABLESTEAMWORKS
        // ═════════════════════════════════════════════════════════════════
        //   各分区绘制
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Steam / Room / Host / Lobby ID / Owner 一行式状态条。</summary>
        private void DrawStatus()
        {
            GUILayout.Label($"Steam: {(SteamManager.Initialized ? "OK" : "NOT INIT")}   " +
                            $"InRoom: {_net.InRoom}   Host: {_net.IsHost}");
            if (_net.InRoom)
            {
                GUILayout.Label($"Lobby: {(ulong)_net.Lobby.CurrentLobby}");
                GUILayout.Label($"Owner: {Name(_net.Lobby.Owner)}");
            }
        }

        /// <summary>
        /// 未入房时的配置表单：lobby 类型、tag、最大人数、按 ID 直接加入。
        /// Create / Find 的时候会把这里的值同步到 LobbyManager。
        /// </summary>
        private void DrawCreateConfig()
        {
            GUILayout.Label("<b>Create config</b>");

            // Type
            GUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(80));
            _typeIdx = GUILayout.SelectionGrid(_typeIdx, LobbyTypeNames, LobbyTypeNames.Length);
            GUILayout.EndHorizontal();

            // Tag
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tag", GUILayout.Width(80));
            _editTag = GUILayout.TextField(_editTag ?? "");
            GUILayout.EndHorizontal();

            // MaxMembers
            GUILayout.BeginHorizontal();
            GUILayout.Label("MaxMembers", GUILayout.Width(80));
            _editMaxMembers = GUILayout.TextField(_editMaxMembers, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            // Join by ID：对方 Invite 时把 lobby id 复制到剪贴板，这里粘贴即可加入，覆盖 overlay 不可用的环境
            GUILayout.BeginHorizontal();
            GUILayout.Label("Join by ID", GUILayout.Width(80));
            _editJoinId = GUILayout.TextField(_editJoinId ?? "");
            GUI.enabled = !string.IsNullOrEmpty(_editJoinId);
            if (GUILayout.Button("Join", GUILayout.Width(60)))
            {
                if (ulong.TryParse(_editJoinId.Trim(), out var raw))
                    _net.JoinRoom(new CSteamID(raw));
                else
                    Log("invalid lobby id");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>入房后的成员区域标题；具体列表在 <see cref="DrawMembers"/>。</summary>
        private void DrawInRoomHeader()
        {
            GUILayout.Label($"<b>Members</b>  (lobby: {SteamMatchmaking.GetNumLobbyMembers(_net.Lobby.CurrentLobby)}, " +
                            $"transport: {_connectedPeers.Count})");
        }

        /// <summary>主要按钮区：Create / Find / Leave / Invite / Broadcast。各按钮按当前 InRoom 状态启停。</summary>
        private void DrawActions()
        {
            // 第一行：房间生命周期
            GUILayout.BeginHorizontal();

            GUI.enabled = !_net.InRoom && SteamManager.Initialized;
            if (GUILayout.Button("Create"))
            {
                _net.Lobby.GameTag = string.IsNullOrEmpty(_editTag) ? "" : _editTag;
                int max = int.TryParse(_editMaxMembers, out var v) && v > 0 ? v : 4;
                _net.Lobby.CreateLobby(max, LobbyTypes[_typeIdx]);
            }
            if (GUILayout.Button("Find"))
            {
                _net.Lobby.GameTag = string.IsNullOrEmpty(_editTag) ? "" : _editTag;
                _net.Lobby.FindLobby();
            }

            GUI.enabled = _net.InRoom;
            if (GUILayout.Button("Leave")) _net.LeaveRoom();
            if (GUILayout.Button("Invite"))
            {
                // Steam overlay 只对从 Steam 启动的进程注入；Editor / 直接双击 .exe 时不弹也属正常。
                // fallback：把 lobby id 写入剪贴板，对端用 Join by ID 粘贴加入。
                SteamFriends.ActivateGameOverlayInviteDialog(_net.Lobby.CurrentLobby);
                var id = ((ulong)_net.Lobby.CurrentLobby).ToString();
                GUIUtility.systemCopyBuffer = id;
                Log($"[Invite] overlay called; lobby id {id} copied to clipboard");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // 第二行：传输测试
            GUILayout.BeginHorizontal();
            GUI.enabled = _net.InRoom && _connectedPeers.Count > 0;
            if (GUILayout.Button("Broadcast Ping (R)")) Broadcast(reliable: true);
            if (GUILayout.Button("Broadcast Ping (U)")) Broadcast(reliable: false);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>Find 结果列表，每行可单点 Join。</summary>
        private void DrawFoundLobbies()
        {
            GUILayout.Label($"<b>Found lobbies</b> ({_foundLobbies.Count})");
            _scrollLobbies = GUILayout.BeginScrollView(_scrollLobbies, GUILayout.Height(110));
            foreach (var lid in _foundLobbies)
            {
                GUILayout.BeginHorizontal();
                int members = SteamMatchmaking.GetNumLobbyMembers(lid);
                GUILayout.Label($"{(ulong)lid}  [{members}]");
                if (GUILayout.Button("Join", GUILayout.Width(60))) _net.JoinRoom(lid);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 当前 lobby 全部成员（从 SteamMatchmaking 实时查询）。
        /// 行前标识：[me] 自己 / [T] transport 已连通 / [ ] 在大厅但 transport 未就绪。
        /// 每个非自身且已连通的成员都可单点定向 Send。
        /// </summary>
        private void DrawMembers()
        {
            _scrollMembers = GUILayout.BeginScrollView(_scrollMembers, GUILayout.Height(140));
            var self = SteamUser.GetSteamID();
            foreach (var m in CurrentMembers())
            {
                GUILayout.BeginHorizontal();
                string flag = m == self ? "[me]"
                    : (_connectedPeers.Contains(m) ? "[T]" : "[ ]");
                GUILayout.Label($"{flag} {Name(m)}");

                GUI.enabled = m != self && _connectedPeers.Contains(m);
                if (GUILayout.Button("R", GUILayout.Width(28))) SendDirect(m, reliable: true);
                if (GUILayout.Button("U", GUILayout.Width(28))) SendDirect(m, reliable: false);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        // ═════════════════════════════════════════════════════════════════
        //   发送辅助
        // ═════════════════════════════════════════════════════════════════

        /// <summary>对所有 transport 已连通的 peer 各发一份 ping。</summary>
        private void Broadcast(bool reliable)
        {
            var msg = $"bcast#{++_pingSeq} t={Time.time:F2}";
            var bytes = Encoding.UTF8.GetBytes(msg);
            int sent = 0;
            foreach (var p in _connectedPeers)
                if (_net.Send(p, bytes, 0, bytes.Length, reliable)) sent++;
            Log($"[Send×{sent}] {msg}");
        }

        /// <summary>对指定 peer 单发；失败（peer 不在 _peerToConn 表里）会显示 FAIL。</summary>
        private void SendDirect(CSteamID target, bool reliable)
        {
            var msg = $"direct#{++_pingSeq} t={Time.time:F2}";
            var bytes = Encoding.UTF8.GetBytes(msg);
            bool ok = _net.Send(target, bytes, 0, bytes.Length, reliable);
            Log($"[Send→{Name(target)} {(ok ? "OK" : "FAIL")}] {msg}");
        }

        // ═════════════════════════════════════════════════════════════════
        //   小工具
        // ═════════════════════════════════════════════════════════════════

        /// <summary>SteamID 配合好友名展示，未知好友退化成纯数字。</summary>
        private static string Name(CSteamID id)
        {
            var n = SteamFriends.GetFriendPersonaName(id);
            return string.IsNullOrEmpty(n) ? id.ToString() : $"{n}({(ulong)id})";
        }

        /// <summary>当前 lobby 的全部成员，按 Steam 的成员索引顺序枚举。</summary>
        private IEnumerable<CSteamID> CurrentMembers()
        {
            var lid = _net.Lobby.CurrentLobby;
            if (lid == CSteamID.Nil) yield break;
            int n = SteamMatchmaking.GetNumLobbyMembers(lid);
            for (int i = 0; i < n; i++) yield return SteamMatchmaking.GetLobbyMemberByIndex(lid, i);
        }
#endif

        /// <summary>同时写到面板日志和 Unity Console。日志最多保留 200 行。</summary>
        private void Log(string s)
        {
            var line = $"{Time.time:F2}  {s}";
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
            Debug.Log("[NetTest] " + s);
        }
    }
}
