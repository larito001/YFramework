using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 战争迷雾服务（移植自 QinZhuo/FogOfWar_ForUnity 的网格思路，接到本项目服务架构）：
///   1. 启动时把一块固定世界区域按 <see cref="TileSize"/> 扫成障碍网格（Physics.CheckBox，撞 <see cref="WallMask"/> 墙）。
///   2. 每隔 <see cref="UpdateInterval"/> 重算一次可见性：以玩家所在格为中心，圆形范围内标可见；范围内的障碍格按距离
///      逐个对"身后小角度锥内、更远的格子"做遮挡（格子级 shadowcasting）。见过的格子记为"已探索"。
///   3. 每格雾浓度：可见=0 / 已探索未见=<see cref="MemoryAlpha"/>(灰) / 从未见=1(黑)；写进一张世界固定的 Texture2D。
///   4. **时间缓动**：每帧把"显示浓度"朝"目标浓度"lerp，雾平滑散开/合拢，不跳变。贴图 Bilinear 采样 + CPU 盒模糊 → 软边。
///   5. 雾贴图盖在一块世界固定 quad 上（UV 0..1 对应整张图）。
///   6. **遮挡剔除**：任意 actor（角色 / 塔 / 宝箱 / 掉落物 / 子弹…）所在格不可见(在雾里) → 关其 Renderer，
///      满足"墙后看不到"。直接复用可见网格，不再逐帧打射线。武器跟随持有者可见性，不按自身位置算。
///
/// 走 ILateTickable，读 view 写完的玩家最新位置。视觉依赖 Resources/Shaders/FogOfWarOverlay；缺失则只做遮挡剔除。
/// 障碍网格是**静态**的（启动扫一次）；墙体移动/增删后调 <see cref="RebuildObstacles"/> 重扫。
/// </summary>
public class FogOfWarManager : IGameService, ILateTickable
{
    // ── 迷雾区域 / 网格 ──
    /// <summary>迷雾覆盖的世界区域中心（XZ）。</summary>
    public Vector2 AreaCenter = Vector2.zero;
    /// <summary>迷雾覆盖的世界区域大小（XZ，米）。玩家活动范围要落在里面。</summary>
    public Vector2 AreaSize = new Vector2(1000f, 1000f);
    /// <summary>网格格子边长（米）。越小越精细、越多格子。</summary>
    public float TileSize = 1f;
    /// <summary>挡视线 / 算障碍的层。默认 Terrain（墙体放 Terrain 层，避免把地面/角色当墙）。</summary>
    public LayerMask WallMask = 0;
    /// <summary>障碍检测盒的中心高度与半高（米）：覆盖墙体的竖直区间，避开贴地的地面碰撞体。</summary>
    public float ObstacleCheckY = 1f;
    public float ObstacleCheckHalfHeight = 1f;

    // ── 视野 / 刷新 ──
    /// <summary>视野半径（米）。</summary>
    public float VisionRadius = 12f;
    /// <summary>可见性重算间隔（秒）。低频即可，配合时间缓动看起来仍然顺滑。</summary>
    public float UpdateInterval = 0.15f;
    /// <summary>时间缓动速度（越大越快跟上目标，雾散合越利落）。</summary>
    public float LerpSpeed = 8f;
    /// <summary>**活动窗口**在视野半径之外额外覆盖的格数（缓动余量）。每帧只处理「玩家所在格 ± (视野格数+本余量)」
    /// 这个矩形窗口内的格子，使每帧 / 每次重算的成本只跟视野大小挂钩、**与地图总格数无关**（1000m 大图也不变贵）。
    /// 余量是为了让格子离开可见范围后仍有若干帧留在窗口内缓动到目标浓度，避免移动时窗口边缘留亮斑残影：
    /// 太小有残影，太大白算格子。经验值 ≈ 玩家速度 × 缓动收敛时间 / TileSize，留点富余。</summary>
    public int ActiveMargin = 10;

    // ── 外观 ──
    /// <summary>雾颜色；a 作为整体浓度倍率。</summary>
    public Color FogColor = new Color(0f, 0f, 0f, 1f);
    /// <summary>是否启用"已探索记忆"（见过的区域离开视野后保留半灰，而非全黑）。</summary>
    public bool ExploredMemory = true;
    /// <summary>已探索未在视野内的雾浓度（0..1，灰度）。</summary>
    public float MemoryAlpha = 0.55f;
    /// <summary>目标浓度的 CPU 盒模糊次数（额外软化边缘）。</summary>
    public int BlurPasses = 1;
    /// <summary>雾面相对区域的高度（米），贴地即可。</summary>
    public float FogHeightOffset = 0.02f;
    /// <summary>是否启用墙后 / 范围外角色的 Renderer 剔除。</summary>
    public bool HideActorsBehindWall = true;

    private GameContext ctx;
    private CharacterManager characterMgr;
    private ActorWorld world;

    // 网格
    private int mapW, mapH;
    private float originX, originZ; // 区域左下角世界坐标
    private bool[] obstacle;
    private bool[] visible;     // 本次重算的可见
    private bool[] visibleTmp;  // 单个 viewer 计算用
    private bool[] explored;
    private float[] target;     // 目标雾浓度 0..1
    private float[] targetTmp;  // 模糊用
    private float[] displayed;  // 显示雾浓度（时间缓动）
    private Color32[] pixels;
    private float updateTimer;
    private bool obstaclesBuilt;

    // 重算用复用 buffer
    private readonly List<int> tilesBuf = new List<int>();
    private readonly List<int> obsBuf = new List<int>();
    private float[] distBuf;
    // 障碍排序比较器缓存：避免 obsBuf.Sort 每次重算分配闭包委托（~6.7 次/秒）。distBuf 是字段，捕获 this 一次性建。
    private Comparison<int> obsCompare;
    // 上一次重算置 visible=true 的格子：下次重算前精确清回 false，避免每帧 Array.Clear 整张 visible。
    private readonly List<int> lastVisibleTiles = new List<int>();

    // ── 活动窗口（只处理玩家周围的格子）──
    private int winX0, winY0, winX1, winY1; // 当前帧活动窗口（含边界，格坐标）
    private bool hasWindow;                 // 玩家是否落在迷雾网格内（否则本帧不处理窗口）
    private int prevWinX0, prevWinY0, prevWinX1, prevWinY1; // 上一帧窗口（检测瞬移/重生跳变用）
    private bool prevWinValid;
    private Color32[] windowPixels;         // 局部纹理上传缓冲（窗口大小），避免每帧 SetPixels32 整张图

    // 渲染
    private Transform root;
    private Texture2D fogTex;
    private Material fogMat;
    private bool visualEnabled;

    // 遮挡剔除缓存
    private readonly List<Actor> actorBuffer = new List<Actor>();
    private readonly Dictionary<int, Renderer[]> rendererCache = new Dictionary<int, Renderer[]>();

    private static readonly int HashColor = Shader.PropertyToID("_Color");
    private static readonly int HashMainTex = Shader.PropertyToID("_MainTex");

    public void Init(GameContext context)
    {
        ctx = context;
        ResolveServices();
        if (WallMask == 0) WallMask = LayerMask.GetMask("Terrain");
        AllocateGrid();
        obsCompare = (a, b) => distBuf[a].CompareTo(distBuf[b]); // 缓存一次，避免每次重算分配委托
        // 障碍网格延迟到首次出现玩家时再扫：FOW 在 bootstrap 早期 Init，那时游戏场景的墙体还没加载。
        BuildRenderObjects();
    }

    public void Shutdown()
    {
        RevealAll();
        if (root != null)Object.Destroy(root.gameObject);
        if (fogTex != null) Object.Destroy(fogTex);
        root = null; fogTex = null; fogMat = null;
        actorBuffer.Clear();
        ctx = null; characterMgr = null; world = null;
    }

    public void LateTick(float dt)
    {
        if (characterMgr == null || world == null) { ResolveServices(); if (characterMgr == null) return; }

        var player = characterMgr.Player;
        if (player == null)
        {
            RevealAll();
            if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }

        // 首次见到玩家：此时游戏场景 + 墙体已加载，扫一次障碍网格。
        if (!obstaclesBuilt) { RebuildObstacles(); obstaclesBuilt = true; updateTimer = UpdateInterval; }

        // 每帧算活动窗口（玩家周围）：StepTemporal 每帧用、RecomputeVisibility 重算帧用，都只动窗口内格子。
        ComputeActiveWindow(player.Position);

        // 重算可见性（低频）
        updateTimer += dt;
        if (updateTimer >= UpdateInterval)
        {
            updateTimer = 0f;
            RecomputeVisibility(player.Position);
        }

        if (visualEnabled)
        {
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            StepTemporal(dt); // 时间缓动 + 上传贴图
        }

        if (HideActorsBehindWall) UpdateActorVisibility(player);
    }

    private void ResolveServices()
    {
        if (ctx == null) return;
        ctx.TryGet(out characterMgr);
        ctx.TryGet(out world);
    }

    // ── 网格 / 坐标 ─────────────────────────────────────────────

    private void AllocateGrid()
    {
        mapW = Mathf.Max(1, Mathf.RoundToInt(AreaSize.x / TileSize));
        mapH = Mathf.Max(1, Mathf.RoundToInt(AreaSize.y / TileSize));
        originX = AreaCenter.x - mapW * TileSize * 0.5f;
        originZ = AreaCenter.y - mapH * TileSize * 0.5f;
        int n = mapW * mapH;
        obstacle = new bool[n];
        visible = new bool[n];
        visibleTmp = new bool[n];
        explored = new bool[n];
        target = new float[n];
        targetTmp = new float[n];
        displayed = new float[n];
        distBuf = new float[n];
        pixels = new Color32[n];
        for (int i = 0; i < n; i++) { displayed[i] = 1f; target[i] = 1f; pixels[i] = new Color32(0, 0, 0, 255); }

        // 局部上传缓冲：最大窗口边长 = 2*(视野格数 + 余量) + 1，面积封顶到 n（小图大视野时）。
        int maxWinSide = 2 * (Mathf.CeilToInt(VisionRadius / Mathf.Max(0.0001f, TileSize)) + Mathf.Max(0, ActiveMargin)) + 1;
        windowPixels = new Color32[Mathf.Min(maxWinSide * maxWinSide, n)];
        prevWinValid = false;
        lastVisibleTiles.Clear();
    }

    /// <summary>用 Physics.CheckBox 把区域扫成障碍网格。墙体移动 / 增删后可手动再调一次。</summary>
    public void RebuildObstacles()
    {
        if (obstacle == null) return;
        var half = new Vector3(TileSize * 0.49f, ObstacleCheckHalfHeight, TileSize * 0.49f);
        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                Vector3 c = new Vector3(originX + (x + 0.5f) * TileSize, ObstacleCheckY, originZ + (y + 0.5f) * TileSize);
                obstacle[x + y * mapW] = Physics.CheckBox(c, half, Quaternion.identity, WallMask, QueryTriggerInteraction.Ignore);
            }
        }
    }

    /// <summary>标记障碍网格为脏：下次见到玩家时重扫一次。**场景切换 / 墙体大改后调用**——
    /// 本服务与雾面是 DontDestroyOnLoad、跨场景存活，<see cref="obstaclesBuilt"/> 不会自动失效，
    /// 否则换图后仍按旧墙体算遮挡 + actor 剔除(#9)。</summary>
    public void MarkObstaclesDirty() => obstaclesBuilt = false;

    private int TileX(int idx) => idx % mapW;
    private int TileY(int idx) => idx / mapW;
    private bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < mapW && y < mapH;

    private bool WorldToTile(Vector3 w, out int tx, out int ty)
    {
        tx = Mathf.FloorToInt((w.x - originX) / TileSize);
        ty = Mathf.FloorToInt((w.z - originZ) / TileSize);
        return InGrid(tx, ty);
    }

    /// <summary>按玩家位置算当前帧活动窗口（玩家所在格 ± (视野格数 + ActiveMargin)，clamp 到网格）。
    /// 每帧 LateTick 调一次，StepTemporal（每帧）/ RecomputeVisibility（重算帧）共用。玩家在网格外则 hasWindow=false。</summary>
    private void ComputeActiveWindow(Vector3 playerPos)
    {
        hasWindow = WorldToTile(playerPos, out int px, out int py);
        if (!hasWindow) return;
        int winR = Mathf.CeilToInt(VisionRadius / TileSize) + Mathf.Max(0, ActiveMargin);
        winX0 = Mathf.Max(0, px - winR);
        winY0 = Mathf.Max(0, py - winR);
        winX1 = Mathf.Min(mapW - 1, px + winR);
        winY1 = Mathf.Min(mapH - 1, py + winR);
    }

    // ── 可见性重算 ───────────────────────────────────────────────

    private void RecomputeVisibility(Vector3 playerPos)
    {
        // 精确清回上次置 true 的可见格（窗口外 visible 恒为 false，actor 剔除据此判定）——不再 Array.Clear 整张图。
        for (int i = 0; i < lastVisibleTiles.Count; i++) visible[lastVisibleTiles[i]] = false;
        lastVisibleTiles.Clear();

        // viewers：当前只有玩家；要加友军/塔在这里再 ComputeViewer 一次即可（同样只动各自视野窗口）。
        if (WorldToTile(playerPos, out int px, out int py))
            ComputeViewer(px, py, VisionRadius / TileSize);

        if (!hasWindow) return;

        // 目标浓度只在活动窗口内重算（窗口外格子静止：要么未探索黑、要么已探索灰，不必每次重算）。
        RecomputeTargetWindow(winX0, winY0, winX1, winY1);

        for (int p = 0; p < BlurPasses; p++) BoxBlurWindow();
    }

    /// <summary>单个 viewer 的可见性：圆形范围 + 障碍角度遮挡，结果并入全局 visible/explored。</summary>
    private void ComputeViewer(int px, int py, float rangeTiles)
    {
        int R = Mathf.Max(1, Mathf.CeilToInt(rangeTiles));
        float r2 = rangeTiles * rangeTiles;

        tilesBuf.Clear();
        obsBuf.Clear();

        for (int j = -R; j <= R; j++)
        {
            int ty = py + j;
            for (int i = -R; i <= R; i++)
            {
                int tx = px + i;
                if (!InGrid(tx, ty)) continue;
                float d2 = i * i + j * j;
                if (d2 > r2) continue;
                int idx = tx + ty * mapW;
                visibleTmp[idx] = true;
                distBuf[idx] = d2;
                tilesBuf.Add(idx);
                if (obstacle[idx]) obsBuf.Add(idx);
            }
        }

        // 障碍按距离近→远遮挡更远的格子（用缓存的比较器，避免每次分配委托）
        obsBuf.Sort(obsCompare);
        for (int o = 0; o < obsBuf.Count; o++)
        {
            int obIdx = obsBuf[o];
            int ox = TileX(obIdx) - px, oy = TileY(obIdx) - py;
            float odsq = ox * ox + oy * oy;
            float z = Mathf.PI / (6f + odsq / 1.2f); // 角度阈值：近障碍遮挡角更大
            float obDist = distBuf[obIdx];
            for (int t = 0; t < tilesBuf.Count; t++)
            {
                int tIdx = tilesBuf[t];
                if (!visibleTmp[tIdx]) continue;
                if (distBuf[tIdx] <= obDist) continue; // 只挡更远的
                int rx = TileX(tIdx) - px, ry = TileY(tIdx) - py;
                if (CantDisplay(ox, oy, rx, ry, z)) visibleTmp[tIdx] = false;
            }
        }

        // 并入全局，记录置 true 的格子（供下次精确清除），并清掉本 viewer 的临时标记
        for (int t = 0; t < tilesBuf.Count; t++)
        {
            int idx = tilesBuf[t];
            if (visibleTmp[idx]) { visible[idx] = true; explored[idx] = true; lastVisibleTiles.Add(idx); }
            visibleTmp[idx] = false;
        }
    }

    /// <summary>格子(x2,y2)是否被障碍(x1,y1)挡住视线（都相对观察者）。移植自 FOWTool.CantDisplay。</summary>
    private static bool CantDisplay(int x1, int y1, int x2, int y2, float z)
    {
        if ((x1 == 0 && y1 == 0) || (x2 == 0 && y2 == 0)) return true;
        if (x1 == 0 || x2 == 0)
        {
            int t = y1; y1 = x1; x1 = t;
            t = y2; y2 = x2; x2 = t;
        }
        float k1 = y1 * 1f / x1;
        float k2 = y2 * 1f / x2;
        float dot = x1 * x2 + y1 * y2;
        if (dot > 0) return Mathf.Abs((k2 - k1) / (1f + k1 * k2)) < z;
        return false;
    }

    /// <summary>盒模糊只在活动窗口内做。先把窗口 target 拷到 targetTmp，再从 targetTmp（窗口内）/ target（窗口外静止值）
    /// 读、写回 target —— **不整组 swap**，否则会用 targetTmp 的脏旧值覆盖窗口外的 target。</summary>
    private void BoxBlurWindow()
    {
        for (int y = winY0; y <= winY1; y++)
            for (int x = winX0; x <= winX1; x++)
            { int i = x + y * mapW; targetTmp[i] = target[i]; }

        for (int y = winY0; y <= winY1; y++)
            for (int x = winX0; x <= winX1; x++)
            {
                float sum = 0f; int cnt = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy; if (yy < 0 || yy >= mapH) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx; if (xx < 0 || xx >= mapW) continue;
                        bool inWin = xx >= winX0 && xx <= winX1 && yy >= winY0 && yy <= winY1;
                        sum += inWin ? targetTmp[xx + yy * mapW] : target[xx + yy * mapW];
                        cnt++;
                    }
                }
                target[x + y * mapW] = sum / cnt;
            }
    }

    // ── 时间缓动 + 上传 ──────────────────────────────────────────

    private void StepTemporal(float dt)
    {
        float k = 1f - Mathf.Exp(-LerpSpeed * dt); // 与帧率无关
        bool dirty = false;

        if (hasWindow)
        {
            // 瞬移 / 重生：新旧窗口完全不相交时，先**重算旧窗口的 target**（玩家已离开 → 不可见 → 已探索灰/黑），
            // 再收敛上传——否则旧窗口仍用"玩家站那里时算的 target=0"，会在旧位置留一块永久全亮无雾(#1)。
            if (prevWinValid &&
                (winX1 < prevWinX0 || winX0 > prevWinX1 || winY1 < prevWinY0 || winY0 > prevWinY1))
            {
                // 旧窗口已被彻底抛下（与新窗口不相交）→ 一律按"不可见"算目标（已探索灰 / 未探索黑），
                // 不读 visible[]：重生那帧 RecomputeVisibility 可能还没把旧位置的 visible 清掉，读它会误判为"可见→全清"。
                SettleTargetWindow(prevWinX0, prevWinY0, prevWinX1, prevWinY1);
                dirty |= WriteWindow(prevWinX0, prevWinY0, prevWinX1, prevWinY1, 1f);
            }

            dirty |= WriteWindow(winX0, winY0, winX1, winY1, k);
            prevWinX0 = winX0; prevWinY0 = winY0; prevWinX1 = winX1; prevWinY1 = winY1;
            prevWinValid = true;
        }
        else if (prevWinValid)
        {
            // #8：玩家离开迷雾网格 → 让最后停留的窗口继续淡出到目标（已不可见 → 已探索灰/黑），否则那块雾会卡在
            // 半淡状态不闭合。同样按"不可见"算目标（玩家已不在格内）。收敛后置 prevWinValid=false 停止每帧刷新。
            SettleTargetWindow(prevWinX0, prevWinY0, prevWinX1, prevWinY1);
            dirty = WriteWindow(prevWinX0, prevWinY0, prevWinX1, prevWinY1, k);
            if (!dirty) prevWinValid = false;
        }

        // #4：仅在确有像素变化时上传一次——玩家静止、雾已收敛则零上传；瞬移帧两块也合并成一次 Apply。
        if (dirty) fogTex.Apply(false);
    }

    /// <summary>窗口内缓动 displayed 朝 target（k=1 即直接收敛 / settle），写 alpha 到 pixels 并 SetPixels32 该窗口块
    /// （区域版，避免每帧拷贝/上传整张图）。返回本窗口是否有像素变化（无变化则跳过 SetPixels32，由调用方决定是否 Apply）。
    /// windowPixels 按需扩容：兼容运行时调大 VisionRadius/ActiveMargin 使窗口超过初始缓冲(#2)，否则会越界。</summary>
    private bool WriteWindow(int x0, int y0, int x1, int y1, float k)
    {
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1, need = bw * bh;
        if (windowPixels == null || windowPixels.Length < need) windowPixels = new Color32[need];

        int w = 0; bool changed = false;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = x + y * mapW;
                displayed[i] += (target[i] - displayed[i]) * k;
                byte a = (byte)(Mathf.Clamp01(displayed[i]) * 255f);
                if (a != pixels[i].a) changed = true;
                pixels[i].a = a;
                windowPixels[w++] = pixels[i]; // 行优先填窗口块（与 SetPixels32 区域版一致：colors[0]→(x0,y0)）
            }
        if (changed) fogTex.SetPixels32(x0, y0, bw, bh, windowPixels);
        return changed;
    }

    /// <summary>重算指定窗口内每格的目标雾浓度：可见=0 / 已探索=MemoryAlpha / 未探索=1。</summary>
    private void RecomputeTargetWindow(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = x + y * mapW;
                if (visible[i]) target[i] = 0f;
                else if (ExploredMemory && explored[i]) target[i] = MemoryAlpha;
                else target[i] = 1f;
            }
    }

    /// <summary>把一个**被抛下的**窗口（瞬移旧址 / 玩家离开网格）的目标浓度按"不可见"算：已探索=MemoryAlpha / 未探索=1，
    /// 不读 visible[]（该窗口玩家已离开，无论 visible 是否还残留旧值都视为不可见）。</summary>
    private void SettleTargetWindow(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = x + y * mapW;
                target[i] = (ExploredMemory && explored[i]) ? MemoryAlpha : 1f;
            }
    }

    // ── 渲染对象 ─────────────────────────────────────────────────

    private void BuildRenderObjects()
    {
        var fogShader = Resources.Load<Shader>("Shaders/FogOfWarOverlay");
        if (fogShader == null)
        {
            Debug.LogWarning("[FogOfWar] 缺少 Resources/Shaders/FogOfWarOverlay，仅启用墙后遮挡剔除，无视觉黑雾。");
            visualEnabled = false;
            return;
        }

        fogTex = new Texture2D(mapW, mapH, TextureFormat.RGBA32, false)
        { name = "FogTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);

        root = new GameObject("FogOfWar").transform;
        Object.DontDestroyOnLoad(root.gameObject);

        var fogGo = new GameObject("FogOverlay");
        fogGo.transform.SetParent(root, false);
        fogGo.transform.position = new Vector3(AreaCenter.x, FogHeightOffset, AreaCenter.y);
        var mf = fogGo.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildQuad(mapW * TileSize, mapH * TileSize);
        var mr = fogGo.AddComponent<MeshRenderer>();
        fogMat = new Material(fogShader);
        fogMat.SetColor(HashColor, FogColor);
        fogMat.SetTexture(HashMainTex, fogTex);
        mr.sharedMaterial = fogMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;

        visualEnabled = true;
    }

    /// <summary>原点居中、朝上的水平 quad，UV 0..1（u→+X，v→+Z），尺寸 sizeX×sizeZ。</summary>
    private static Mesh BuildQuad(float sizeX, float sizeZ)
    {
        float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
        var m = new Mesh { name = "FogQuad" };
        m.vertices = new[]
        {
            new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz),
            new Vector3( hx, 0f,  hz), new Vector3( hx, 0f, -hz),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.bounds = new Bounds(Vector3.zero, new Vector3(sizeX, 1f, sizeZ));
        return m;
    }

    // ── 遮挡剔除 ─────────────────────────────────────────────────

    /// <summary>每帧给所有 actor 写 <see cref="Actor.Visible"/> 字段（和 TimeScaleZoneService 写 ZoneScale 同构）。
    /// **本服务只写字段、不碰 Renderer**——渲染剔除由各 view 在自己的 LateUpdate 里读 Owner.Visible 自行消费
    /// （见 BaseView.ApplyFogVisibility / WeaponView）。这样任何新 actor 类型零成本接入，且没有"manager 反向操作别人
    /// renderer"的双权威 + 缓存 + 类型特例。</summary>
    private void UpdateActorVisibility(Character player)
    {
        actorBuffer.Clear();
        world.AppendAll(actorBuffer);

        // pass 1：独立定位的 actor（角色 / 塔 / 宝箱 / 掉落物 / 子弹…）按自身所在格是否可见。玩家恒可见；
        // Weapon 是挂在持有者身上的跟随型 actor，位置由持有者驱动，留到 pass 2 按持有者算。
        for (int i = 0; i < actorBuffer.Count; i++)
        {
            var a = actorBuffer[i];
            if (a == null) continue;
            if (a is Weapon) continue;
            a.Visible = a.ID == player.ID || IsWorldPosVisible(a.Position);
        }

        // pass 2：武器只跟随持有者的**迷雾可见性**（不掺装备态——装备/卸下由 WeaponView 自己叠加）。
        // 无主武器记 Visible=true，由 WeaponView 的装备态决定显隐。
        for (int i = 0; i < actorBuffer.Count; i++)
        {
            if (!(actorBuffer[i] is Weapon w)) continue;
            w.Visible = w.OwnerActorId < 0
                || (world.TryGet(w.OwnerActorId, out var owner) && owner != null && owner.Visible);
        }
    }

    /// <summary>世界坐标当前是否在视野内。落在网格外视为可见（那里没有雾）。</summary>
    private bool IsWorldPosVisible(Vector3 pos)
    {
        if (!WorldToTile(pos, out int tx, out int ty)) return true;
        return visible[tx + ty * mapW];
    }

    /// <summary>迷雾关闭 / 无玩家时，把所有 actor 的 Visible 复位为 true；各 view 下一帧 LateUpdate 自行恢复 renderer。</summary>
    private void RevealAll()
    {
        if (world == null) return;
        actorBuffer.Clear();
        world.AppendAll(actorBuffer);
        for (int i = 0; i < actorBuffer.Count; i++)
            if (actorBuffer[i] != null) actorBuffer[i].Visible = true;
    }
}
