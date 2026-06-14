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
///   6. **遮挡剔除**：角色所在格不可见(在雾里) → 关其 Renderer，满足"墙后看不到"。直接复用可见网格，不再逐帧打射线。
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
    public Vector2 AreaSize = new Vector2(100f, 100f);
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
    private ViewManager viewMgr;

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

    // 渲染
    private Transform root;
    private Texture2D fogTex;
    private Material fogMat;
    private bool visualEnabled;

    // 遮挡剔除缓存
    private readonly List<Actor> actorBuffer = new List<Actor>();
    private readonly Dictionary<int, Renderer[]> rendererCache = new Dictionary<int, Renderer[]>();
    private readonly Dictionary<int, bool> charVisible = new Dictionary<int, bool>();

    private static readonly int HashColor = Shader.PropertyToID("_Color");
    private static readonly int HashMainTex = Shader.PropertyToID("_MainTex");

    public void Init(GameContext context)
    {
        ctx = context;
        ResolveServices();
        if (WallMask == 0) WallMask = LayerMask.GetMask("Terrain");
        AllocateGrid();
        // 障碍网格延迟到首次出现玩家时再扫：FOW 在 bootstrap 早期 Init，那时游戏场景的墙体还没加载。
        BuildRenderObjects();
    }

    public void Shutdown()
    {
        RevealAll();
        if (root != null)Object.Destroy(root.gameObject);
        if (fogTex != null) Object.Destroy(fogTex);
        root = null; fogTex = null; fogMat = null;
        rendererCache.Clear();
        actorBuffer.Clear();
        ctx = null; characterMgr = null; world = null; viewMgr = null;
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
        ctx.TryGet(out viewMgr);
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

    private int TileX(int idx) => idx % mapW;
    private int TileY(int idx) => idx / mapW;
    private bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < mapW && y < mapH;

    private bool WorldToTile(Vector3 w, out int tx, out int ty)
    {
        tx = Mathf.FloorToInt((w.x - originX) / TileSize);
        ty = Mathf.FloorToInt((w.z - originZ) / TileSize);
        return InGrid(tx, ty);
    }

    // ── 可见性重算 ───────────────────────────────────────────────

    private void RecomputeVisibility(Vector3 playerPos)
    {
        Array.Clear(visible, 0, visible.Length);

        // viewers：当前只有玩家；要加友军/塔在这里追加格子坐标即可。
        if (WorldToTile(playerPos, out int px, out int py))
            ComputeViewer(px, py, VisionRadius / TileSize);

        // 目标浓度：可见=0 / 已探索=MemoryAlpha / 未探索=1
        for (int i = 0; i < target.Length; i++)
        {
            if (visible[i]) target[i] = 0f;
            else if (ExploredMemory && explored[i]) target[i] = MemoryAlpha;
            else target[i] = 1f;
        }

        for (int p = 0; p < BlurPasses; p++) BoxBlur();
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

        // 障碍按距离近→远遮挡更远的格子
        obsBuf.Sort((a, b) => distBuf[a].CompareTo(distBuf[b]));
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

        // 并入全局，并清掉本 viewer 的临时标记
        for (int t = 0; t < tilesBuf.Count; t++)
        {
            int idx = tilesBuf[t];
            if (visibleTmp[idx]) { visible[idx] = true; explored[idx] = true; }
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

    private void BoxBlur()
    {
        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                float sum = 0f; int cnt = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy; if (yy < 0 || yy >= mapH) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx; if (xx < 0 || xx >= mapW) continue;
                        sum += target[xx + yy * mapW]; cnt++;
                    }
                }
                targetTmp[x + y * mapW] = sum / cnt;
            }
        }
        var t = target; target = targetTmp; targetTmp = t;
    }

    // ── 时间缓动 + 上传 ──────────────────────────────────────────

    private void StepTemporal(float dt)
    {
        float k = 1f - Mathf.Exp(-LerpSpeed * dt); // 与帧率无关
        for (int i = 0; i < displayed.Length; i++)
        {
            displayed[i] += (target[i] - displayed[i]) * k;
            pixels[i].a = (byte)(Mathf.Clamp01(displayed[i]) * 255f);
        }
        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);
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

    private void UpdateActorVisibility(Character player)
    {
        actorBuffer.Clear();
        world.AppendAll(actorBuffer);

        // pass 1：角色。玩家恒可见；其余看其所在格本帧是否可见。
        charVisible.Clear();
        charVisible[player.ID] = true;
        for (int i = 0; i < actorBuffer.Count; i++)
        {
            var a = actorBuffer[i];
            if (a == null || a.ID == player.ID || !(a is Character)) continue;

            bool vis = IsWorldPosVisible(a.Position);
            charVisible[a.ID] = vis;
            var rends = GetRenderers(a.ID);
            if (rends != null) SetRenderersEnabled(rends, vis);
        }

        // pass 2：武器跟随持有者可见性（已装备 且 持有者可见）。
        for (int i = 0; i < actorBuffer.Count; i++)
        {
            var a = actorBuffer[i];
            if (!(a is Weapon w)) continue;
            bool ownerVisible = w.OwnerActorId >= 0 && charVisible.TryGetValue(w.OwnerActorId, out var ov) && ov;
            bool vis = w.IsEquipped && ownerVisible;
            var rends = GetRenderers(a.ID);
            if (rends != null) SetRenderersEnabled(rends, vis);
        }
    }

    /// <summary>世界坐标当前是否在视野内。落在网格外视为可见（那里没有雾）。</summary>
    private bool IsWorldPosVisible(Vector3 pos)
    {
        if (!WorldToTile(pos, out int tx, out int ty)) return true;
        return visible[tx + ty * mapW];
    }

    private void RevealAll()
    {
        if (world == null) return;
        actorBuffer.Clear();
        world.AppendAll(actorBuffer);
        for (int i = 0; i < actorBuffer.Count; i++)
        {
            var a = actorBuffer[i];
            if (a == null) continue;
            var rends = GetRenderers(a.ID);
            if (rends != null) SetRenderersEnabled(rends, true);
        }
    }

    private Renderer[] GetRenderers(int id)
    {
        if (rendererCache.TryGetValue(id, out var cached))
        {
            if (cached != null && cached.Length > 0 && cached[0] != null) return cached;
            rendererCache.Remove(id);
        }
        if (viewMgr != null && viewMgr.TryGetView(id, out var view) && view != null)
        {
            var rr = view.GetComponentsInChildren<Renderer>(true);
            rendererCache[id] = rr;
            return rr;
        }
        return null;
    }

    private static void SetRenderersEnabled(Renderer[] rends, bool on)
    {
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null && rends[i].enabled != on) rends[i].enabled = on;
    }
}
