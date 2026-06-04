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
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 340;
        var view = root.AddComponent<TaskCardView>();

        // 标题(左上)
        var title = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(0, 1); titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(40, -30); titleRt.sizeDelta = new Vector2(760, 104);
        var titleTmp = NewText(title, "标题", 46, TitleText, TextAlignmentOptions.Left);

        // 进度(右上,富文本,不省略)
        var prog = NewUI("Progress", out var progRt, root.transform);
        progRt.anchorMin = new Vector2(1, 1); progRt.anchorMax = new Vector2(1, 1); progRt.pivot = new Vector2(1, 1);
        progRt.anchoredPosition = new Vector2(-30, -30); progRt.sizeDelta = new Vector2(520, 104);
        var progTmp = NewText(prog, "进度", 38, TitleText, TextAlignmentOptions.Right);
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
