#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成任务卡片预制体 TaskCard.prefab。<see cref="TaskPanel"/> 运行时 instantiate + 绑定(<see cref="TaskCardView"/>)。
/// 横向单行(内容垂直居中):左侧目标图标(白图占位)+ 任务描述 + 进度条(美术 Slider_02_LightGreen,横向拉伸)+ 奖励(小底+图标+数量)+ 领取/未完成 按钮。
/// 高 400(LayoutElement);宽由列表 VerticalLayoutGroup 撑满(≈1772),故标题/进度条用左右锚拉伸、奖励/按钮用右锚定位。
/// 字号注意:UITheme.Font 会再 ×2,且 TMP 字号超过框高会整段不渲染,卡内文字基准要小。
/// 菜单:Tools/UI/Build TaskCard Prefab
/// </summary>
public static class TaskCardBuilder
{
    private const string Dir = "Assets/Resources/UI/Task";
    private const string PrefabPath = Dir + "/TaskCard.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private static TMP_FontAsset _font;

    private const string FrameDir = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Frame/CardFrame/";
    private const string FrameBg = FrameDir + "CardFrame_04_White_Bg.png";
    private const string FrameInnerBorder = FrameDir + "CardFrame_04_White_InnerBorder.png";
    private const string FrameBorder = FrameDir + "CardFrame_04_White_Border.png";
    private const string BtnDir = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Button/";
    private const string BtnBg = BtnDir + "Button_01_White_Bg.Png";
    private const string BtnInnerBorder = BtnDir + "Button_01_White_InnerBorder1.Png";
    private const string SliderPath = "Assets/Art/UI/NewUI/Theme_Blue/Prefabs/Prefabs_Slider/Slider_02_LightGreen.prefab";
    private const string RewardBgPath = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Frame/BasicFrame/BasicFrame_Circle_H96_White_Bg.png";

    private static readonly Color TitleColor = new Color(0.10f, 0.10f, 0.12f, 1f);   // 任务描述:近黑
    private static readonly Color RewardBgColor = new Color(0.86f, 0.86f, 0.84f, 1f); // 奖励小底:浅灰

    [MenuItem("Tools/UI/Build TaskCard Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var root = NewUI("TaskCard", out var rootRt);
        rootRt.sizeDelta = new Vector2(820, 400);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.97f, 0.97f, 0.95f, 1f); // 白卡
        bg.sprite = LoadSprite(FrameBg);
        bg.type = Image.Type.Sliced;
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 400;
        var view = root.AddComponent<TaskCardView>();

        // 卡面边框(白底 + 黑边)
        Skin("InnerBorder", root.transform, FrameInnerBorder, new Color(0.69f, 0.886f, 1f, 1f), Stretch);
        Skin("Border", root.transform, FrameBorder, Color.black, Stretch);

        // 左侧目标图标(白图占位;配表 iconPath 填了再换)
        var objIconGo = NewUI("ObjIcon", out var objIconRt, root.transform);
        objIconRt.anchorMin = objIconRt.anchorMax = new Vector2(0, 0.5f); objIconRt.pivot = new Vector2(0.5f, 0.5f);
        objIconRt.anchoredPosition = new Vector2(135, 0); objIconRt.sizeDelta = new Vector2(190, 190);
        var objIconImg = objIconGo.AddComponent<Image>();
        objIconImg.color = Color.white; objIconImg.preserveAspect = true; objIconImg.raycastTarget = false;

        // 任务描述(横向拉伸,中心偏上;近黑,不省略)
        var title = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 0.5f); titleRt.anchorMax = new Vector2(1, 0.5f); titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.offsetMin = new Vector2(260, 16); titleRt.offsetMax = new Vector2(-460, 96);
        var titleTmp = NewText(title, "标题", 27.5f, TitleColor, TextAlignmentOptions.Left);
        titleTmp.overflowMode = TextOverflowModes.Overflow;

        // 进度条(美术 Slider_02_LightGreen;横向拉伸,中心偏下;浅绿填充 + 内置 X/Y)
        var sliderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPath);
        var sliderGo = (GameObject)PrefabUtility.InstantiatePrefab(sliderPrefab, root.transform);
        sliderGo.name = "ProgressBar";
        ((RectTransform)sliderGo.transform).localScale = Vector3.one;
        var sliderRt = (RectTransform)sliderGo.transform;
        sliderRt.anchorMin = new Vector2(0, 0.5f); sliderRt.anchorMax = new Vector2(1, 0.5f); sliderRt.pivot = new Vector2(0.5f, 0.5f);
        sliderRt.offsetMin = new Vector2(260, -96); sliderRt.offsetMax = new Vector2(-460, -16);
        var slider = sliderGo.GetComponent<Slider>();
        if (slider != null) { slider.interactable = false; slider.minValue = 0; slider.maxValue = 1; slider.value = 1; }
        var sliderText = sliderGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (sliderText != null) { sliderText.text = "0/0"; sliderText.fontSize = UITheme.Font(35.25f); sliderText.color = Color.white; sliderText.overflowMode = TextOverflowModes.Overflow; }

        // 奖励(右锚,挨着按钮:小圆底 + 图标 + ×数量[白字,反推自预制体])
        var rewardWrap = NewUI("Reward", out var rewardRt, root.transform);
        rewardRt.anchorMin = rewardRt.anchorMax = new Vector2(1, 0.5f); rewardRt.pivot = new Vector2(0.5f, 0.5f);
        rewardRt.anchoredPosition = new Vector2(-355, 0); rewardRt.sizeDelta = new Vector2(130, 230);
        var rBgGo = NewUI("Bg", out var rBgRt, rewardWrap.transform);
        rBgRt.anchorMin = rBgRt.anchorMax = rBgRt.pivot = new Vector2(0.5f, 1f);
        rBgRt.anchoredPosition = new Vector2(0, -8); rBgRt.sizeDelta = new Vector2(112, 112);
        var rBgImg = rBgGo.AddComponent<Image>();
        rBgImg.sprite = LoadSprite(RewardBgPath); rBgImg.type = Image.Type.Sliced; rBgImg.color = RewardBgColor; rBgImg.raycastTarget = false;
        var rIconGo = NewUI("Icon", out var rIconRt, rewardWrap.transform);
        rIconRt.anchorMin = rIconRt.anchorMax = rIconRt.pivot = new Vector2(0.5f, 1f);
        rIconRt.anchoredPosition = new Vector2(0, -24); rIconRt.sizeDelta = new Vector2(84, 84);
        var rIconImg = rIconGo.AddComponent<Image>();
        rIconImg.preserveAspect = true; rIconImg.raycastTarget = false;
        var rCntGo = NewUI("Count", out var rCntRt, rewardWrap.transform);
        rCntRt.anchorMin = rCntRt.anchorMax = rCntRt.pivot = new Vector2(0.5f, 1f);
        rCntRt.anchoredPosition = new Vector2(-7, -136.98f); rCntRt.sizeDelta = new Vector2(170.115f, 93.0156f);
        var rCntTmp = NewText(rCntGo, "×0", 27.5f, Color.black, TextAlignmentOptions.Center);

        // 右侧按钮(领取/未完成;美术皮肤,状态色染 Bg。右锚,加大不挤)
        var act = NewUI("Action", out var actRt, root.transform);
        actRt.anchorMin = actRt.anchorMax = new Vector2(1, 0.5f); actRt.pivot = new Vector2(1, 0.5f);
        actRt.anchoredPosition = new Vector2(-40, -10.3974f); actRt.sizeDelta = new Vector2(230, 129.2051f);
        var actImg = act.AddComponent<Image>();
        actImg.color = new Color(0.96f, 0.66f, 0.18f, 1f);
        var actBtn = act.AddComponent<Button>();
        actBtn.targetGraphic = actImg;
        var actSkinBg = Skin("Bg", act.transform, BtnBg, new Color(1f, 0.80392164f, 0.1764706f, 1f), Stretch);
        var actInner = Skin("InnerBorder1", act.transform, BtnInnerBorder, new Color(0.9686275f, 0.9215687f, 0f, 1f), null);
        var actInnerRt = (RectTransform)actInner.transform;
        actInnerRt.anchorMin = Vector2.zero; actInnerRt.anchorMax = Vector2.one;
        actInnerRt.anchoredPosition = new Vector2(-0.24319458f, 1.1852989f); actInnerRt.sizeDelta = new Vector2(-8.5063f, -8.4427f);
        var actLabel = NewUI("Label", out var actLabelRt, act.transform);
        Stretch(actLabelRt);
        var actLabelTmp = NewText(actLabel, "前往", 22, Color.white, TextAlignmentOptions.Center);
        actLabelTmp.overflowMode = TextOverflowModes.Overflow;

        view.bg = bg;
        view.objectiveIcon = objIconImg;
        view.titleText = titleTmp;
        view.progressSlider = slider;
        view.progressText = sliderText;
        view.rewardIcon = rIconImg;
        view.rewardCount = rCntTmp;
        view.actionBg = actSkinBg;
        view.actionButton = actBtn;
        view.actionLabel = actLabelTmp;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TaskCardBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[TaskCardBuilder] 找不到精灵 {path}");
        return s;
    }

    private static Image Skin(string name, Transform parent, string spritePath, Color color, System.Action<RectTransform> layout)
    {
        var go = NewUI(name, out var rt, parent);
        layout?.Invoke(rt);
        var img = go.AddComponent<Image>();
        img.sprite = LoadSprite(spritePath);
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = color; tmp.raycastTarget = false;
        tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
