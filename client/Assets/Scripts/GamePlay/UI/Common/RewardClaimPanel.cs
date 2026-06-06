using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>奖励种类:道具(走背包/item 配表)或货币(走钱包/currency 配表)。</summary>
public enum RewardKind
{
    Item,
    Currency,
}

/// <summary>单条奖励:种类 + id(道具=itemId,货币=(int)CurrencyType)+ 数量。用工厂方法构造避免填错字段。</summary>
public class RewardEntry
{
    public RewardKind kind;
    public int id;
    public long count;

    /// <summary>道具奖励(itemId + 数量)。</summary>
    public static RewardEntry Item(int itemId, long count = 1)
        => new RewardEntry { kind = RewardKind.Item, id = itemId, count = count };

    /// <summary>货币奖励(币种 + 数量)。</summary>
    public static RewardEntry Currency(CurrencyType type, long count)
        => new RewardEntry { kind = RewardKind.Currency, id = (int)type, count = count };
}

/// <summary>
/// 通用奖励领取弹窗参数:一组奖励 + 标题 + 自动消失时长 + 是否由弹窗代发。
/// </summary>
public class RewardClaimParam
{
    public string title = "获得奖励";
    public List<RewardEntry> rewards = new List<RewardEntry>();

    /// <summary>展示多少秒后自动消失(&lt;=0 视为不自动关,需外部 Hide)。默认 1 秒。</summary>
    public float autoCloseSeconds = 1f;

    /// <summary>
    /// true:弹窗负责把奖励发放到钱包/背包(货币 <see cref="CurrencySystem.Add"/>、道具 <see cref="BagSystem.AddItem"/>);
    /// false:仅展示(发放由调用方在 Show 前自行完成)。默认仅展示,避免与调用方重复发放。
    /// </summary>
    public bool grant = false;
}

/// <summary>
/// 通用奖励领取弹窗(<see cref="UIEnum.RewardClaimPanel"/>,Top 层)。横排展示一组奖励
/// (图标 + ×数量 + 名称),弹出后默认 1 秒自动消失。道具走 <see cref="BagSystem"/>/item 配表,
/// 货币走 <see cref="CurrencySystem"/>/currency 配表取图标与名称;<see cref="RewardClaimParam.grant"/>
/// 为 true 时由本弹窗代发到钱包/背包。任意界面可复用:
/// <code>
/// Show&lt;RewardClaimPanel, RewardClaimParam&gt;(new RewardClaimParam {
///     rewards = { RewardEntry.Currency(CurrencyType.Gold, 100), RewardEntry.Item(1001, 2) },
///     grant = true,
/// });
/// </code>
/// 预制体外壳由 <c>Tools/UI/Build RewardClaimPanel Prefab</c> 生成,运行时按 rewards 构建奖励格。
/// </summary>
public class RewardClaimPanel : UIPageBase<RewardClaimParam>
{
    [Header("外壳(由 Builder 接好)")]
    public TextMeshProUGUI titleText;
    public RectTransform rewardContainer; // HorizontalLayoutGroup 容器,运行时填奖励格
    public Sprite goldRewardIcon;         // 金币奖励整图(getCoin;在 Art 下故烘入引用)
    public Sprite energyRewardIcon;       // 体力奖励整图(getHeart)

    private BagSystem bag;
    private CurrencySystem currency;
    private ResMgr resMgr;
    private TMP_FontAsset font;
    private ICoroutineRunner runner;
    private Coroutine autoCloseCo;

    public override void OnLoad()
    {
        bag = GetService<BagSystem>();
        currency = GetService<CurrencySystem>();
        resMgr = GetService<ResMgr>();
        runner = GetService<ICoroutineRunner>();
        if (titleText != null) font = titleText.font; // 复用外壳中文字体给运行时格子
    }

    protected override void OnBeforeShow(RewardClaimParam param)
    {
        var p = param ?? new RewardClaimParam();
        if (titleText != null) titleText.text = string.IsNullOrEmpty(p.title) ? "获得奖励" : p.title;

        if (p.grant) GrantAll(p.rewards);
        RebuildRewards(p.rewards);
    }

    public override void OnShow()
    {
        // 默认 1 秒后自动消失;autoCloseSeconds <= 0 表示不自动关(由外部 Hide)。
        float delay = PageParam != null ? PageParam.autoCloseSeconds : 1f;
        if (delay > 0f) autoCloseCo = runner?.Run(CloseAfter(delay));
    }

    public override void OnHide()
    {
        if (autoCloseCo != null) { runner?.Stop(autoCloseCo); autoCloseCo = null; }
        ClearContainer();
    }

    public override void OnResize() { }

    private IEnumerator CloseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        autoCloseCo = null;
        CloseSelf();
    }

    // ---------------- 发放(可选) ----------------

    private void GrantAll(List<RewardEntry> rewards)
    {
        if (rewards == null) return;
        foreach (var r in rewards)
        {
            if (r == null || r.count <= 0) continue;
            if (r.kind == RewardKind.Currency) currency?.Add((CurrencyType)r.id, r.count);
            else bag?.AddItem(r.id, (int)r.count);
        }
    }

    // ---------------- 奖励格构建 ----------------

    private void RebuildRewards(List<RewardEntry> rewards)
    {
        ClearContainer();
        if (rewardContainer == null || rewards == null) return;
        foreach (var r in rewards)
        {
            if (r == null || r.count <= 0) continue;
            BuildCell(r);
        }
    }

    private void ClearContainer()
    {
        if (rewardContainer == null) return;
        for (int i = rewardContainer.childCount - 1; i >= 0; i--)
            Destroy(rewardContainer.GetChild(i).gameObject);
    }

    private void BuildCell(RewardEntry r)
    {
        // 格容器:宽高由 LayoutElement 提供给 HorizontalLayoutGroup,容器自身在白色内容框内居中。
        // 奖励居中显示:上图标 + 下数量(不显示名称)。
        var cell = NewChild(rewardContainer, $"Reward_{r.kind}_{r.id}", out _);
        var le = cell.AddComponent<LayoutElement>();
        le.preferredWidth = 360; le.preferredHeight = 440;

        // 图标(上部,水平居中)
        var icon = NewChild(cell.transform, "Icon", out var iconRt);
        iconRt.anchorMin = new Vector2(0.5f, 1f); iconRt.anchorMax = new Vector2(0.5f, 1f); iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0, -30); iconRt.sizeDelta = new Vector2(290, 290); // 底部到 y≈120
        var iconImg = icon.AddComponent<Image>();
        iconImg.raycastTarget = false; iconImg.preserveAspect = true;
        BindIconAsync(iconImg, r); // 货币用专用整图(金币/体力),道具优先 3D 模型快照、回退 2D 图标

        // 数量(图标正下方,居中,×N,醒目金色)。框高 90 + Overflow,放大字号也完整显示。
        var count = NewChild(cell.transform, "Count", out var countRt);
        countRt.anchorMin = new Vector2(0, 0); countRt.anchorMax = new Vector2(1, 0); countRt.pivot = new Vector2(0.5f, 0);
        countRt.offsetMin = new Vector2(0, 20); countRt.offsetMax = new Vector2(0, 110);
        NewText(count, $"×{r.count}", 48, TextAlignmentOptions.Center, new Color(0.10f, 0.10f, 0.12f, 1f)); // 黑色数字(衬白色内容框)
        var countTmp = count.GetComponent<TextMeshProUGUI>();
        if (countTmp != null) countTmp.overflowMode = TextOverflowModes.Overflow; // 不裁切,放大字号也完整显示
    }

    /// <summary>把奖励图标贴到 <paramref name="img"/>:金币/体力优先用 Builder 接入的奖励整图(getCoin/getHeart);
    /// 其它货币走 currency 配表 IconPath;道具优先 3D 模型侧视快照(武器只有 ModelPath 也能显示),无模型回退 2D 图标。</summary>
    private void BindIconAsync(Image img, RewardEntry r)
    {
        if (img == null) return;

        if (r.kind == RewardKind.Currency)
        {
            var type = (CurrencyType)r.id;
            // 金币/体力:优先用 Builder 接入的奖励整图(getCoin/getHeart),无需异步加载
            Sprite direct = type == CurrencyType.Gold ? goldRewardIcon
                          : type == CurrencyType.Energy ? energyRewardIcon : null;
            if (direct != null) { img.sprite = direct; img.enabled = true; return; }

            string path = (type == CurrencyType.Gold || type == CurrencyType.Energy)
                ? CurrencyIcon.ResPath(type)
                : (currency != null ? currency.IconPath(type) : null);
            img.sprite = null; img.enabled = false;
            if (resMgr == null || string.IsNullOrEmpty(path)) return;
            resMgr.LoadAsync<Sprite>(path, sp =>
            {
                if (img == null) { if (sp != null) resMgr.Release<Sprite>(path); return; }
                img.sprite = sp; img.enabled = sp != null;
            });
            return;
        }

        var item = bag?.GetItem(r.id);
        ModelSnapshotCache.BindCardImageAsync(img, item?.ModelPath, item?.IconPath, resMgr);
    }

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private void NewText(GameObject go, string text, float size, TextAlignmentOptions align, Color color)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}