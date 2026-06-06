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

    // 资源胶囊统一背景:美术九宫格图 + 纯黑半透明底。所有面板的资源胶囊都走 ApplyBackground,改背景只动这两行即可全局生效。
    public const string PillBgSpritePath = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Slider/Slider_Swipe_01_Bg.png";
    private static readonly Color PillColor = new Color(0f, 0f, 0f, 0.5294118f); // 纯黑 α≈53%(与主界面 Energy 胶囊同款)

    /// <summary>
    /// 新建一个完整资源胶囊(自带深色底 + 图标 + 数值),返回数值文本(运行时只填数字)。
    /// anchor 同时作为 anchorMin/Max/pivot(取某个角,如右上 (1,1))。
    /// </summary>
    public static TextMeshProUGUI Build(Transform parent, string name, CurrencyType type, TMP_FontAsset font,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, float fontSize = 44f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var bg = go.GetComponent<Image>();
        ApplyBackground(bg);

        var valueGo = new GameObject("Value", typeof(RectTransform));
        var vrt = (RectTransform)valueGo.transform;
        vrt.SetParent(go.transform, false);
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero; vrt.offsetMax = new Vector2(-28, 0);
        var tmp = valueGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "0";
        tmp.fontSize = UITheme.Font(fontSize);
        tmp.alignment = TextAlignmentOptions.Right;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;                 // 数字位数多也不换行,横向延展
        tmp.overflowMode = TextOverflowModes.Overflow;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;

        AddIconLeft(go, vrt, type, 72f, 16f);
        return tmp;
    }

    /// <summary>给资源胶囊底图 Image 应用统一背景:美术九宫格 sprite + 纯黑半透明 + Sliced。
    /// 所有面板(主界面/商店/装备/任务/图鉴/地图)的资源胶囊底都调这里,想换背景只改 <see cref="PillBgSpritePath"/> 与 <c>PillColor</c>。</summary>
    public static void ApplyBackground(Image bg)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PillBgSpritePath);
        if (sprite != null) bg.sprite = sprite;
        bg.type = Image.Type.Sliced;
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
            }
        }
    }
}
#endif
