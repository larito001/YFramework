#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 各面板顶部「资源条」统一构件:深色圆角胶囊 + 左侧货币图标 + 右对齐数值,**不写「金币/体力」文字**。
/// 与主界面 <see cref="StartPanelBuilder"/> 的胶囊同款,供任务/商店/图鉴/地图/装备等面板共用,保证视觉统一。
/// 图标不再烤进预制体:Icon 上挂 <see cref="CurrencyIconBinder"/>,运行时按币种从 Resources/UI/Icons 动态加载(方便换图)。
/// </summary>
public static class UICurrencyPill
{
    // 货币种类(图标运行时按种类从 Resources 动态加载,见 CurrencyIcon / CurrencyIconBinder)
    public const CurrencyType IconGold   = CurrencyType.Gold;
    public const CurrencyType IconEnergy = CurrencyType.Energy;

    // 资源框统一背景:美术整图 equipTitleBlock(黑底 + 绿边)。所有面板的资源框都走 ApplyBackground,改背景只动这两行即可全局生效。
    public const string PillBgSpritePath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/equipTitleBlock.png";
    private static readonly Color PillColor = Color.white; // 显示整图本色(不再叠黑半透明)

    /// <summary>
    /// 新建一个完整资源胶囊(深色底 + 左图标 + 数值),返回数值文本(运行时只填数字)。
    /// anchor 同时作为 anchorMin/Max/pivot(取某个角,如右上 (1,1));<paramref name="size"/>.y 为胶囊高度,
    /// **宽度由 ContentSizeFitter 按内容自适应**——数字位数再多也不超框(取 pivot 反方向延展:右上锚点向左长,左上锚点向右长)。
    /// </summary>
    public static TextMeshProUGUI Build(Transform parent, string name, CurrencyType type, TMP_FontAsset font,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, float fontSize = 44f, float minWidth = 280f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size; // 高度用 size.y;宽度被下面的 ContentSizeFitter 覆盖为内容宽度

        var bg = go.GetComponent<Image>();
        ApplyBackground(bg);

        // 横向布局:图标 + 数值;胶囊宽度随数值长度自适应,数字再长也不超框。
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 12;
        hlg.padding = new RectOffset(24, 32, 0, 0); // 左右留圆角端内边距
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 图标:布局只占「一个胶囊高」的方形槽(背景宽度据此自适应,不会被放大的图标撑长);
        // 视觉放大交给子 Image,超出槽位的部分是图标四周透明光晕,overhang 不挡数字。
        // 奖励弹窗图标在 RewardClaimPanel 自管,不受此影响。
        float iconSlot = size.y;          // 布局footprint(决定背景长度)
        float iconVisual = size.y * 2.0f; // 图标视觉放大(想更大改这里,不会再撑长背景)
        var ig = new GameObject("Icon", typeof(RectTransform), typeof(LayoutElement));
        ig.transform.SetParent(go.transform, false);
        var ile = ig.GetComponent<LayoutElement>();
        ile.minWidth = ile.preferredWidth = iconSlot;
        ile.minHeight = ile.preferredHeight = iconSlot;
        var imgGo = new GameObject("Img", typeof(RectTransform), typeof(Image), typeof(CurrencyIconBinder));
        var irt = (RectTransform)imgGo.transform;
        irt.SetParent(ig.transform, false);
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(iconVisual, iconVisual);
        var im = imgGo.GetComponent<Image>();
        im.preserveAspect = true; im.raycastTarget = false;
        imgGo.GetComponent<CurrencyIconBinder>().type = type;

        // 数值(TMP 按内容报告 preferred 宽度,胶囊据此撑开;居中,短数字时两侧留白均衡)
        var valueGo = new GameObject("Value", typeof(RectTransform), typeof(LayoutElement));
        valueGo.transform.SetParent(go.transform, false);
        var tmp = valueGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "0";
        tmp.fontSize = UITheme.Font(fontSize);
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;

        // 数值区最小宽度:短数字(如体力 62)也保持像样的胶囊长度,避免过短难看。
        // 总最小宽度 minWidth 扣掉左右内边距(24+32)、图标间距(12)与图标本身,余下给数值区。
        var vle = valueGo.GetComponent<LayoutElement>();
        vle.minWidth = Mathf.Max(0f, minWidth - (24 + 32 + 12 + iconSlot));

        return tmp;
    }

    /// <summary>给资源框底图 Image 应用统一背景:美术整图 sprite(本色显示)。
    /// 所有面板(主界面/商店/装备/任务/图鉴/地图)的资源框底都调这里,想换背景只改 <see cref="PillBgSpritePath"/> 与 <c>PillColor</c>。</summary>
    public static void ApplyBackground(Image bg)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PillBgSpritePath);
        if (sprite != null) bg.sprite = sprite;
        bg.type = Image.Type.Simple; // equipTitleBlock 非九宫格整图,用 Simple 拉伸
        bg.color = PillColor;
    }

    /// <summary>在已有胶囊里(左侧竖直居中)加一个货币图标,并把右对齐的数值文本左边距让开图标。
    /// 图标 Sprite 不烤进预制体——挂 <see cref="CurrencyIconBinder"/>,运行时按币种从 Resources 动态加载。</summary>
    public static void AddIconLeft(GameObject pill, RectTransform valueText, CurrencyType type, float iconSize = 64f, float pad = 14f)
    {
        var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(CurrencyIconBinder));
        var irt = (RectTransform)ig.transform;
        irt.SetParent(pill.transform, false);
        irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
        irt.pivot = new Vector2(0, 0.5f);
        irt.anchoredPosition = new Vector2(pad, 0f);
        irt.sizeDelta = new Vector2(iconSize, iconSize);
        var im = ig.GetComponent<Image>();
        im.preserveAspect = true;
        im.raycastTarget = false;
        ig.GetComponent<CurrencyIconBinder>().type = type; // 运行时从 Resources/UI/Icons 动态加载对应图标

        if (valueText != null)
        {
            valueText.anchorMin = Vector2.zero; valueText.anchorMax = Vector2.one; // 确保拉伸,offsetMin 才生效
            valueText.offsetMin = new Vector2(pad + iconSize + 10f, valueText.offsetMin.y);
            var tmp = valueText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.enableWordWrapping = false;             // 数字位数多也不换行,横向延展
                tmp.overflowMode = TextOverflowModes.Overflow;
                // 数字过长(如金币上万)时自动缩字号适配胶囊宽度,避免「超框」。所有走 AddIconLeft 的胶囊统一生效。
                tmp.fontSizeMax = tmp.fontSize;
                tmp.fontSizeMin = Mathf.Max(1f, tmp.fontSize * 0.5f);
                tmp.enableAutoSizing = true;
            }
        }
    }
}
#endif
