using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 背包面板（<see cref="UIPageBase"/>，注册为 <see cref="UIEnum.BagPanel"/>）。
/// 用 <see cref="YOTOScrollView"/> 池化渲染所有槽位，点击格子=使用物品；监听 <see cref="YOTOEventType.RefreshBagList"/>
/// 自动刷新（<see cref="BagSystem"/> 任意增删改后都会发该事件）。
///
/// 数据全部来自 <see cref="BagSystem"/>（背后是配表 + protobuf，不碰 ScriptableObject）。
/// 图标按物品配表 IconPath 用 ResMgr 加载，缓存避免重复 Load。
///
/// 预制体结构（在 Unity 搭好，路径 UI/Bag/BagPanel，挂本组件）：
///   BagPanel (UIPageBase 必需的 CanvasGroup + YOTOUIShow)
///     ├─ ScrollView (YOTOScrollView，itemPrefab 指向 BagItemView 预制体)
///     ├─ SortBtn    (Button) → sortBtn
///     ├─ CloseBtn   (Button) → closeBtn
///     └─ CapacityText (TextMeshProUGUI) → capacityText（可选，显示 已用/总数）
/// </summary>
public class BagPanel : UIPageBase
{
    [Header("引用")]
    public YOTOScrollView scrollView;
    public Button sortBtn;
    public Button closeBtn;
    public TextMeshProUGUI capacityText;

    [Header("配置")]
    [Tooltip("对象池大小，建议 >= 一屏可见格子数 + 一行冗余")]
    public int poolSize = 40;

    private BagSystem bagSystem;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private bool initialized;

    // 图标缓存：IconPath -> Sprite，避免每次刷新都 Resources.Load
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    public override void OnLoad()
    {
        bagSystem = GetService<BagSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();

        if (scrollView != null)
        {
            scrollView.Initialize(poolSize);
            scrollView.SetRenderer(RenderSlot);
            initialized = true;
        }

        if (sortBtn != null) sortBtn.onClick.AddListener(OnClickSort);
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        eventMgr.Add(YOTOEventType.RefreshBagList, Refresh);
        Refresh();
    }

    public override void OnHide()
    {
        eventMgr.Remove(YOTOEventType.RefreshBagList, Refresh);
    }

    public override void OnResize() { }

    // ---------------- 刷新 ----------------

    private void Refresh()
    {
        if (!initialized || bagSystem?.Bag == null) return;

        var bag = bagSystem.Bag;
        scrollView.SetData(bag.Capacity);

        if (capacityText != null)
            capacityText.text = $"{bag.UsedSlots}/{bag.Capacity}";
    }

    /// <summary>滚动视图渲染回调：把第 index 个槽位的数据塞进复用出来的格子。</summary>
    private void RenderSlot(YOTOScrollViewItem item, int index)
    {
        if (item is not BagItemView view || bagSystem?.Bag == null) return;

        var stack = bagSystem.Bag.GetSlot(index);
        var cfg = stack != null ? bagSystem.GetItem(stack.itemId) : null;
        var sprite = cfg != null ? LoadIcon(cfg.IconPath) : null;

        view.Bind(index, stack, cfg, sprite, OnClickSlot);
    }

    // ---------------- 交互 ----------------

    private void OnClickSlot(int slot)
    {
        // 默认行为：使用该格物品。UseItem 内部会校验是否可用 + 是否注册了处理器，
        // 成功后扣除并发 RefreshBagList，本面板随即自动刷新。
        bagSystem.UseItem(slot);
    }

    private void OnClickSort() => bagSystem.SortBag();

    // ---------------- 图标加载 ----------------

    private Sprite LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (iconCache.TryGetValue(path, out var cached)) return cached;
        var sprite = resMgr.Load<Sprite>(path);
        iconCache[path] = sprite; // 即使为 null 也缓存，避免反复 Load 报错刷屏
        return sprite;
    }
}
