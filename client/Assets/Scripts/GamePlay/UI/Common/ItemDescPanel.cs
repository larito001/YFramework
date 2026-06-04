using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 通用道具描述弹窗参数:传 <see cref="itemId"/> 从 item 配表取名称/描述/图标;
/// 也可用 <see cref="title"/>/<see cref="description"/> 直接覆盖,用于非配表场景。
/// </summary>
public class ItemDescParam
{
    public int itemId;          // >0:从 item 配表读取名称/描述/图标
    public string title;        // 可选:覆盖名称(留空则用配表名)
    public string description;  // 可选:覆盖描述(留空则用配表描述)
}

/// <summary>
/// 通用道具描述弹窗(<see cref="UIEnum.ItemDescPanel"/>,Top 层):点道具图标打开,展示道具名称 + 详细描述 + 图标。
/// 任意界面可复用:<c>Show&lt;ItemDescPanel, ItemDescParam&gt;(new ItemDescParam { itemId = id });</c>
/// 点「关闭」按钮或点窗口外的暗底即可关闭。预制体外壳由 <c>Tools/UI/Build ItemDescPanel Prefab</c> 生成。
/// </summary>
public class ItemDescPanel : UIPageBase<ItemDescParam>
{
    [Header("外壳(由 Builder 接好)")]
    public Image iconImage;          // 道具图标(3D 模型快照优先,无则 2D 图标)
    public TextMeshProUGUI nameText; // 道具名称
    public TextMeshProUGUI descText; // 详细描述(自动换行)
    public Button closeBtn;          // 关闭按钮
    public Button backgroundBtn;     // 点窗口外暗底关闭

    private ConfigManager config;
    private ResMgr resMgr;

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        resMgr = GetService<ResMgr>();
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSelf);
        if (backgroundBtn != null) backgroundBtn.onClick.AddListener(CloseSelf);
    }

    protected override void OnBeforeShow(ItemDescParam param)
    {
        var p = param ?? new ItemDescParam();
        Item item = p.itemId > 0 && config != null ? config.itemConfig.Get((uint)p.itemId) : null;

        string name = !string.IsNullOrEmpty(p.title)
            ? p.title
            : (item != null && !string.IsNullOrEmpty(item.Name) ? item.Name : (p.itemId > 0 ? $"道具{p.itemId}" : ""));
        string desc = !string.IsNullOrEmpty(p.description) ? p.description : (item != null ? item.Desc : "");

        if (nameText != null) nameText.text = name;
        if (descText != null) descText.text = string.IsNullOrEmpty(desc) ? "暂无描述" : desc;

        if (iconImage != null)
        {
            // 与商店/装备/奖励卡一致:武器/镜/弹优先用 3D 模型侧视快照,其它回退 2D 图标
            Sprite sprite = item != null
                ? (ModelSnapshotCache.CardSprite(item.ModelPath, resMgr)
                   ?? (!string.IsNullOrEmpty(item.IconPath) ? resMgr.Load<Sprite>(item.IconPath) : null))
                : null;
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }
    }

    public override void OnShow() { }
    public override void OnHide() { }
    public override void OnResize() { }
}
