using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 游戏内打猎 HUD(<see cref="UIEnum.GameMainPanel"/>):出发进入对局后显示,常驻覆盖在场景之上。
///   左上:当前地图名称 + 本局积分     右上:资源金币
///   中部:瞄准镜准星 + 镜外黑边遮罩(点「瞄准」显示,相机同时变焦放大)
///   底部中:瞄准/射击 圆钮(同一个,文字随状态切换) + 剩余子弹;左下:结束打猎(→ 确认 → 结算)
/// 玩法流程:点瞄准 → 拖屏移动准星(<see cref="CameraSwipeLook"/>)→ 点射击。射击从屏幕中心打射线
/// (<see cref="ScopeAimController.FireRay"/>):命中动物则加该动物击杀积分并播死亡动画,没打中不加分;射击后自动关镜。
/// 弹药容量取自所选子弹装备(<see cref="LoadoutSystem"/> 选中子弹的 MaxStack)。
/// 预制体由 <c>Tools/UI/Build GameMainPanel Prefab</c> 生成。
/// </summary>
public class GameMainPanel : UIPageBase
{
    [Header("顶部")]
    public TextMeshProUGUI mapNameText;
    public TextMeshProUGUI scoreText;

    [Header("中部")]
    public GameObject scope;     // 瞄准镜准星(瞄准时显示)
    public GameObject scopeMask; // 瞄准镜黑边遮罩(铺满屏幕,瞄准时显示;中间圆孔透出场景)

    [Header("底部")]
    public Button actionBtn;            // 中间圆钮:未瞄准时=「瞄准」,瞄准时=「射击」
    public TextMeshProUGUI actionLabel; // 圆钮文字,随瞄准状态切换
    public TextMeshProUGUI ammoText;
    public Button endBtn;               // 结束打猎(左下):弹确认框 → 结算

    private LoadoutSystem loadout;
    private ConfigManager config;
    private EventMgr eventMgr;
    private MapSystem maps;               // 当前选中关卡(显示关卡名)
    private TaskProgressSystem taskProgress; // 击杀计入任务进度("击杀任意动物"类任务)
    private CodexSystem codex;            // 击杀的动物解锁图鉴
    private ILeaderboardService leaderboard; // 对局结束提交本局总分到排行榜
    private ScopeAimController scopeAim; // 相机端瞄准机制(变焦 + 命中射线),挂在主相机上

    private int score;
    private int ammo;
    private bool aiming;
    private readonly Dictionary<int, int> kills = new(); // animalId → 本局击杀数,结束时组装结算明细

    private const float TapMoveThreshold = 20f; // 像素:按下到抬起位移小于此值算「点击」,否则算拖拽
    private bool pointerDown;
    private Vector2 pointerDownPos;

    public override void OnLoad()
    {
        loadout = GetService<LoadoutSystem>();
        config = GetService<ConfigManager>();
        eventMgr = GetService<EventMgr>();
        maps = GetService<MapSystem>();
        taskProgress = GetService<TaskProgressSystem>();
        codex = GetService<CodexSystem>();
        Context.TryGet<ILeaderboardService>(out leaderboard);
        if (actionBtn != null) actionBtn.onClick.AddListener(OnActionClick);
        if (endBtn != null) endBtn.onClick.AddListener(OnEndHunt);
    }

    public override void OnShow()
    {
        // 恢复 HUD 显示(上一局结束打猎的尸检镜头里把它淡出过)
        if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }

        score = 0;
        kills.Clear();
        ammo = InitialAmmo();
        SetAiming(false); // 复位:收起准星/黑边遮罩,相机回到正常视野
        if (mapNameText != null) mapNameText.text = !string.IsNullOrEmpty(maps?.SelectedName) ? maps.SelectedName : "未知关卡";

        RefreshScore();
        RefreshAmmo();
    }

    public override void OnHide()
    {
    }

    public override void OnResize() { }

    // ---------------- 操作 ----------------

    /// <summary>中间圆钮:未瞄准时点击=进入瞄准,瞄准时点击=开火。</summary>
    private void OnActionClick()
    {
        if (!aiming) SetAiming(true);
        else Shoot();
    }

    /// <summary>进入/退出瞄准:切准星 + 黑边遮罩 + 相机变焦,并把圆钮在「瞄准/射击」间切换。</summary>
    private void SetAiming(bool on)
    {
        aiming = on;
        if (scope != null) scope.SetActive(on);
        if (scopeMask != null) scopeMask.SetActive(on);
        ScopeAim()?.SetAiming(on);
        RefreshActionButton();
    }

    /// <summary>瞄准时:点击(非拖拽)镜外任意区域 → 退出瞄准回正常视角。拖拽是环视(交给 CameraSwipeLook),点按钮交给按钮。</summary>
    private void Update()
    {
        if (!aiming) return;

        if (Input.GetMouseButtonDown(0))
        {
            pointerDown = true;
            pointerDownPos = Input.mousePosition;
        }
        else if (pointerDown && Input.GetMouseButtonUp(0))
        {
            pointerDown = false;
            bool isTap = ((Vector2)Input.mousePosition - pointerDownPos).sqrMagnitude <= TapMoveThreshold * TapMoveThreshold;
            if (isTap && !IsPointerOverUI()) SetAiming(false); // 点击镜外(非 UI)空白 → 退出瞄准
        }
    }

    private void Shoot()
    {
        if (!aiming) return;
        eventMgr?.Trigger(YOTOEventType.Shoot); // 开枪反馈(实弹/空枪干打都触发:镜头抖动 + 后座)

        if (ammo <= 0) { RefreshAmmo(); return; } // 空枪:只震屏,不开火、不关镜

        ammo--;

        // 从屏幕中心(准星处)打射线:按部位结算——头/心脏一枪致命,身体两枪;只有「这一枪打死了」才加分计数
        var aim = ScopeAim();
        var shot = aim != null ? aim.FireRay() : default;
        var hit = shot.entity;
        if (hit != null && !hit.IsDead)
        {
            bool died = hit.Hit(shot.zone);
            if (died)
            {
                score += hit.score;
                kills.TryGetValue(hit.animalId, out var c);
                kills[hit.animalId] = c + 1; // 记一笔,供结算逐种统计
                taskProgress?.AddKill(1);    // 计入"击杀任意动物"类任务进度
                codex?.Discover(hit.animalId); // 击杀的动物解锁图鉴
                RefreshScore();
            }
            else
            {
                // 身体中了一枪但没死:给个「命中」飘字,免得玩家以为打空了
                GetService<FlyTextMgr>()?.AddTextAtScreenCenter("命中");
            }
        }

        SetAiming(false); // 实弹射击后关闭瞄准镜
        RefreshAmmo();
    }

    /// <summary>结束打猎:先弹确认框,确认后退出瞄准并打开结算界面。</summary>
    private void OnEndHunt()
    {
        SetAiming(false); // 收起瞄准镜再弹窗
        Show<ConfirmPanel, ConfirmParam>(new ConfirmParam
        {
            title = "结束打猎",
            message = "确定结束本次打猎并查看结算？",
            onConfirm = BeginEndSequence,
        });
    }

    /// <summary>
    /// 结束打猎确认后的检视序列:清掉场上活物 → 在地面摆出本局所有尸体 → 相机抬起俯视成果 → 弹结算界面。
    /// 期间关掉环视、隐藏手持枪、淡出 HUD;相机由 <see cref="InspectThenResult"/> 协程接管。
    /// </summary>
    private void BeginEndSequence()
    {
        SetAiming(false);

        var cam = Camera.main;
        var look = cam != null ? cam.GetComponent<CameraSwipeLook>() : null;
        if (look != null) look.enabled = false; // 尸检镜头自己控制相机,先关环视
        var vm = cam != null ? cam.GetComponent<FpsWeaponViewModel>() : null;
        if (vm != null) vm.SetForceHidden(true); // 隐藏手持枪

        // 淡出 HUD,留干净的检视画面(协程跑在 ICoroutineRunner 上,不受面板显隐影响)
        if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }

        var animals = GetService<AnimalSystem>();
        var bounds = animals != null ? animals.SpawnCorpses(kills) : new Bounds(Vector3.zero, Vector3.one);

        var runner = GetService<ICoroutineRunner>();
        if (runner != null && cam != null) runner.Run(InspectThenResult(cam, bounds));
        else ShowResult(); // 拿不到相机/runner 就直接结算,保证流程不卡
    }

    /// <summary>相机先抬高到尸体上方俯视,再缓慢推近(镜头逐渐拉近)检视成果,最后打开结算界面。</summary>
    private IEnumerator InspectThenResult(Camera cam, Bounds bounds)
    {
        Vector3 fromP = cam.transform.position;
        Quaternion fromR = cam.transform.rotation;

        Vector3 center = bounds.center;
        Vector3 e = bounds.extents;

        // 按尸体堆的实际横/纵尺寸分别算取景距离(竖屏 + 窄列布局 → 横向很窄,可以离得更近)。
        // 屏幕横向 ≈ 世界 X 宽度;屏幕纵向 ≈ 俯视下的纵深 Z(再带一点高度 Y)。取两者所需距离的大者并留 12% 边距。
        float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.1f, cam.aspect));
        float horiz = Mathf.Max(e.x, 0.6f);
        float vert  = Mathf.Max(e.z * 0.85f + e.y, 0.6f);
        float distH = horiz / Mathf.Tan(hHalf);
        float distV = vert / Mathf.Tan(vHalf);
        float fitDist = Mathf.Max(Mathf.Max(distH, distV) * 1.12f, 6f); // 留点边 + 一个最近下限

        Vector3 dir = new Vector3(0f, 1f, -0.85f).normalized; // 俯视方向(上 + 后),竖屏稍微放平一点看清整列
        Vector3 farP  = center + dir * (fitDist * 1.6f);      // 远景:更远
        Vector3 nearP = center + dir * fitDist;               // 近景:恰好框住全部尸体,不再更近
        Quaternion farR = Quaternion.LookRotation((center - farP).normalized, Vector3.up);

        // 第一段:从当前机位抬起到俯视远景
        const float riseDur = 1.2f;
        for (float t = 0f; t < riseDur; t += Time.deltaTime)
        {
            
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / riseDur));
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromP, farP, u), Quaternion.Slerp(fromR, farR, u));
            yield return null;
        }

        // 第二段:缓慢推近到「恰好框住全部」的距离,始终对着尸体中心,画面逐渐拉近又不裁掉动物
        const float pushDur = 3f;
        for (float t = 0f; t < pushDur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / pushDur));
            Vector3 p = Vector3.Lerp(farP, nearP, u);
            Quaternion r = Quaternion.LookRotation((center - p).normalized, Vector3.up);
            cam.transform.SetPositionAndRotation(p, r);
            yield return null;
        }
        ShowResult();
    }

    /// <summary>把本局逐种击杀 + 总分组装成 <see cref="HuntResult"/> 交给结算界面。</summary>
    private void ShowResult()
    {
        leaderboard?.Submit(score); // 提交本局总分到排行榜(best-effort;未登录/未接入则内部跳过)
        maps?.RecordScore(maps.SelectedMapId, score); // 记本关历史最高,用于解锁下一关
        var result = new HuntResult { totalScore = score };
        foreach (var kv in kills)
        {
            var def = config?.animalConfig.Get((uint)kv.Key);
            result.entries.Add(new HuntResultEntry
            {
                animalName = def != null ? def.Name : $"动物 {kv.Key}",
                count = kv.Value,
                score = kv.Value * (def != null ? def.Score : 0),
                prefabPath = def != null ? def.Prefab : null, // 结算页用它离屏渲染动物 3D 图标
            });
        }
        result.entries.Sort((a, b) => b.score.CompareTo(a.score)); // 分高的在前
        Show<FinishPanel, HuntResult>(result);
    }

    /// <summary>懒取主相机上的瞄准机制组件(进对局时由 GameStartScene 挂上)。</summary>
    private ScopeAimController ScopeAim()
    {
        if (scopeAim == null && Camera.main != null)
            scopeAim = Camera.main.GetComponent<ScopeAimController>();
        return scopeAim;
    }

    // ---------------- 刷新 ----------------

    private void RefreshScore()
    {
        if (scoreText != null) scoreText.text = $"当前对局获得积分：{score}";
    }

    private void RefreshAmmo()
    {
        if (ammoText != null) ammoText.text = $"剩余子弹：{ammo}";
        RefreshActionButton();
    }

    /// <summary>圆钮文字随状态切换;始终可点(空枪干打也要给震屏反馈)。</summary>
    private void RefreshActionButton()
    {
        if (actionLabel != null) actionLabel.text = aiming ? "射击" : "瞄准";
        if (actionBtn != null) actionBtn.interactable = true;
    }

    private static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return es.IsPointerOverGameObject();
    }

    /// <summary>弹匣容量:选中子弹装备的 MaxStack;取不到则默认 10。</summary>
    private int InitialAmmo()
    {
        int bulletId = loadout != null ? loadout.GetSelected(ShopCategory.Bullet) : 0;
        if (bulletId > 0 && config != null)
        {
            var it = config.itemConfig.Get((uint)bulletId);
            if (it != null && it.MaxStack > 0) return it.MaxStack;
        }
        return 10;
    }
}
