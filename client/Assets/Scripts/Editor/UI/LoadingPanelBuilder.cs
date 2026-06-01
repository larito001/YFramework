#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成加载页预制体 LoadingPanel.prefab 到 Resources/UI/Boot 下,供 UIMgr.ShowLoading 按路径加载。
/// 程序化构建:Unity 自动解析脚本 GUID / TMP 字体,脚本字段在此接好,避免手写 .prefab YAML。
///
/// 竖屏手机布局(画布 CanvasScaler match=width、refWidth=1920,竖屏高度按机型约 3000+ 单位,故用比例锚点纵向分布):
///   ~70% 高:游戏标题
///   ~46% 高:转圈(LoadingPanel 每帧旋转)
///   ~36% 高:加载中…(省略号循环)
///   ~12% 高:底部小贴士(随机一条)
/// 全屏深色背景 + CanvasGroup + YOTOUIShow(淡入淡出),纯展示无交互。
///
/// 菜单:Tools/UI/Build LoadingPanel Prefab
/// </summary>
public static class LoadingPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Boot";
    private const string PrefabPath = Dir + "/LoadingPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build LoadingPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // ---------- 根(全屏 + 深色背景 + CanvasGroup + YOTOUIShow + LoadingPanel)----------
        var root = NewUI("LoadingPanel", out var rootRt);
        Stretch(rootRt);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.07f, 0.10f, 1f); // 不透明深色,盖住底下种子值/场景
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>(); // 进入/退出淡入淡出(默认配置)

        // ---------- 标题(上方约 70% 高)----------
        var titleGo = NewUI("Title", out var titleRt, root.transform);
        Anchor(titleRt, new Vector2(0.5f, 0.70f), new Vector2(1600, 240));
        var titleText = NewText(titleGo, "昼探夜守", 140, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;

        // ---------- 转圈(屏幕中央偏上)----------
        var spinnerGo = NewUI("Spinner", out var spinnerRt, root.transform);
        Anchor(spinnerRt, new Vector2(0.5f, 0.46f), new Vector2(200, 200));
        var spinnerImg = spinnerGo.AddComponent<Image>();
        spinnerImg.sprite = BuiltinSprite("UI/Skin/Knob.psd"); // 占位转圈图;有美术资源时换成专用 loading 图
        spinnerImg.color = new Color(0.95f, 0.78f, 0.35f, 1f);
        spinnerImg.raycastTarget = false;

        // ---------- 加载中…(转圈下方)----------
        var loadingGo = NewUI("LoadingText", out var loadingRt, root.transform);
        Anchor(loadingRt, new Vector2(0.5f, 0.36f), new Vector2(800, 90));
        var loadingText = NewText(loadingGo, "加载中", 56, TextAlignmentOptions.Center);

        // ---------- 底部小贴士 ----------
        var tipGo = NewUI("Tip", out var tipRt, root.transform);
        Anchor(tipRt, new Vector2(0.5f, 0.12f), new Vector2(1500, 220));
        var tipText = NewText(tipGo, "", 40, TextAlignmentOptions.Center);
        tipText.color = new Color(0.75f, 0.78f, 0.85f, 1f);
        tipText.enableWordWrapping = true;

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<LoadingPanel>();
        panel.uiType = UIEnum.LoadingPanel;
        panel.canvasGroup = cg;
        panel.spinner = spinnerRt;
        panel.loadingText = loadingText;
        panel.tipText = tipText;
        panel.tips = new List<string>
        {
            "提示：每次进图消耗 1 点体力。",
            "提示：打中猎物，结算后按积分获得金币。",
            "提示：金币可在商店购买更好的枪械与子弹。",
            "提示：进度会自动保存到当前存档槽。",
        };

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LoadingPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>按比例锚点把元素居中钉在屏幕的某个高度处(竖屏自适应:锚点随画布高度移动)。</summary>
    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }

    /// <summary>加载 Unity 内置 UI 精灵(失败返回 null,Image 退化为纯色块)。</summary>
    private static Sprite BuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }
}
#endif
