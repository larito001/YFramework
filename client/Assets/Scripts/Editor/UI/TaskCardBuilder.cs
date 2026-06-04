#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成任务卡片预制体 TaskCard.prefab 到 Resources/UI/Task 下。<see cref="TaskPanel"/> 运行时 instantiate + 绑定
/// (<see cref="TaskCardView"/>)。奖励格预置 3 个(固定位),默认隐藏。卡片长相在这里改。
/// 高 340(LayoutElement),宽由 VerticalLayoutGroup 撑满。菜单:Tools/UI/Build TaskCard Prefab
/// </summary>
public static class TaskCardBuilder
{
    private const string Dir = "Assets/Resources/UI/Task";
    private const string PrefabPath = Dir + "/TaskCard.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private static TMP_FontAsset _font;

    // NewUI 共享美术套件:卡面边框(CardFrame_04_White)+ 按钮皮肤(Button_01_White)。九宫格 Sliced。
    private const string FrameDir = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Frame/CardFrame/";
    private const string FrameBg = FrameDir + "CardFrame_04_White_Bg.png";
    private const string FrameInnerBorder = FrameDir + "CardFrame_04_White_InnerBorder.png";
    private const string FrameBorder = FrameDir + "CardFrame_04_White_Border.png";
    private const string FrameTitleBg = FrameDir + "CardFrame_04_White_TitleBg.png";
    private const string FrameTitleBorder = FrameDir + "CardFrame_04_White_TitleBorder.png";
    private const string BtnDir = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Button/";
    private const string BtnBg = BtnDir + "Button_01_White_Bg.Png";
    private const string BtnInnerBorder = BtnDir + "Button_01_White_InnerBorder1.Png";

    private static readonly Color CardFrame = new Color(0.90f, 0.89f, 0.93f, 1f);
    private static readonly Color TitleText = new Color(0.25f, 0.24f, 0.30f, 1f);

    [MenuItem("Tools/UI/Build TaskCard Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var root = NewUI("TaskCard", out var rootRt);
        rootRt.sizeDelta = new Vector2(820, 340);
        var bg = root.AddComponent<Image>();
        bg.color = CardFrame;
        bg.sprite = LoadSprite(FrameBg);     // NewUI 卡底
        bg.type = Image.Type.Sliced;
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 340;
        var view = root.AddComponent<TaskCardView>();

        // 卡面边框装饰(NewUI CardFrame_04_White)。先建 = 渲染在标题/进度/奖励之下
        Skin("InnerBorder", root.transform, FrameInnerBorder, new Color(0.6901961f, 0.8862746f, 1f, 1f), Stretch);
        var titleBg = Skin("TitleBg", root.transform, FrameTitleBg, new Color(0.45098042f, 0.6627451f, 0.75294125f, 1f), null);
        SetTopLeftRect(titleBg, new Vector2(405.17f, -55.6353f), new Vector2(810.33f, 111.2706f));
        var titleBorder = Skin("TitleBorder", root.transform, FrameTitleBorder, Color.black, null);
        SetTopLeftRect(titleBorder, new Vector2(404.9f, -55.5951f), new Vector2(809.81f, 111.19f));
        Skin("Border", root.transform, FrameBorder, Color.black, Stretch);

        // 标题(左上)
        var title = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(0, 1); titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(46, -5); titleRt.sizeDelta = new Vector2(760, 104);
        var titleTmp = NewText(title, "标题", 46, Color.white, TextAlignmentOptions.Left);

        // 进度(右上,富文本,不省略)
        var prog = NewUI("Progress", out var progRt, root.transform);
        progRt.anchorMin = new Vector2(1, 1); progRt.anchorMax = new Vector2(1, 1); progRt.pivot = new Vector2(1, 1);
        progRt.anchoredPosition = new Vector2(-30, -5); progRt.sizeDelta = new Vector2(520, 104);
        var progTmp = NewText(prog, "进度", 38, Color.white, TextAlignmentOptions.Right);
        progTmp.overflowMode = TextOverflowModes.Overflow; progTmp.richText = true;

        // 3 个奖励格(固定位 x=40/408/776),默认隐藏
        var roots = new GameObject[3];
        var icons = new Image[3];
        var counts = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            float x = 40 + 368 * i;
            var slot = NewUI($"Reward{i}", out var slotRt, root.transform);
            slotRt.anchorMin = new Vector2(0, 0); slotRt.anchorMax = new Vector2(0, 0); slotRt.pivot = new Vector2(0, 0);
            slotRt.anchoredPosition = new Vector2(x, 48); slotRt.sizeDelta = new Vector2(336, 88);

            var icon = NewUI("Icon", out var iconRt, slot.transform);
            iconRt.anchorMin = new Vector2(0, 0); iconRt.anchorMax = new Vector2(0, 0); iconRt.pivot = new Vector2(0, 0);
            iconRt.anchoredPosition = Vector2.zero; iconRt.sizeDelta = new Vector2(88, 88);
            var iconImg = icon.AddComponent<Image>();
            iconImg.preserveAspect = true; iconImg.raycastTarget = false;

            var cnt = NewUI("Count", out var cntRt, slot.transform);
            cntRt.anchorMin = new Vector2(0, 0); cntRt.anchorMax = new Vector2(0, 0); cntRt.pivot = new Vector2(0, 0);
            cntRt.anchoredPosition = new Vector2(96, 0); cntRt.sizeDelta = new Vector2(240, 88);
            var cntTmp = NewText(cnt, "×0", 36, TitleText, TextAlignmentOptions.Left);

            slot.SetActive(false);
            roots[i] = slot; icons[i] = iconImg; counts[i] = cntTmp;
        }

        // 右下按钮
        var act = NewUI("Action", out var actRt, root.transform);
        actRt.anchorMin = new Vector2(1, 0); actRt.anchorMax = new Vector2(1, 0); actRt.pivot = new Vector2(1, 0);
        actRt.anchoredPosition = new Vector2(-40, 44); actRt.sizeDelta = new Vector2(240, 120);
        var actImg = act.AddComponent<Image>();
        actImg.color = new Color(0.96f, 0.66f, 0.18f, 1f);
        var actBtn = act.AddComponent<Button>();
        actBtn.targetGraphic = actImg;
        // 按钮皮肤(NewUI Button_01_White),盖在纯色底上;顺序在 Label 之前
        Skin("Bg", act.transform, BtnBg, new Color(1f, 0.80392164f, 0.1764706f, 1f), Stretch);
        var actInner = Skin("InnerBorder1", act.transform, BtnInnerBorder, new Color(0.9686275f, 0.9215687f, 0f, 1f), null);
        var actInnerRt = (RectTransform)actInner.transform;
        actInnerRt.anchorMin = Vector2.zero; actInnerRt.anchorMax = Vector2.one;
        actInnerRt.anchoredPosition = new Vector2(-0.24319458f, 1.1852989f); actInnerRt.sizeDelta = new Vector2(-8.5063f, -8.4427f);
        var actLabel = NewUI("Label", out var actLabelRt, act.transform);
        Stretch(actLabelRt);
        var actLabelTmp = NewText(actLabel, "前往", 40, Color.white, TextAlignmentOptions.Center);

        view.bg = bg;
        view.titleText = titleTmp;
        view.progressText = progTmp;
        view.rewardRoots = roots;
        view.rewardIcons = icons;
        view.rewardCounts = counts;
        // 金币/体力图标是工程内静态 Sprite(非 Resources),烤进预制体供运行时奖励格引用(与顶部资源胶囊同款)
        view.coinIcon = LoadIcon(UICurrencyPill.IconGold);
        view.energyIcon = LoadIcon(UICurrencyPill.IconEnergy);
        view.actionBg = actImg;
        view.actionButton = actBtn;
        view.actionLabel = actLabelTmp;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TaskCardBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static Sprite LoadIcon(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[TaskCardBuilder] 找不到奖励图标 {path}");
        return s;
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[TaskCardBuilder] 找不到精灵 {path}");
        return s;
    }

    // 装饰用九宫格图片(NewUI 美术套件)。layout 可为 Stretch(撑满)或 null(由调用方设矩形)。
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

    // 左上锚 + 居中 pivot 的矩形(标题底/标题框用,值取自手调预制体)
    private static void SetTopLeftRect(Image img, Vector2 anchoredPos, Vector2 size)
    {
        var rt = (RectTransform)img.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
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
