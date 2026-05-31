#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成开始界面预制体 StartPanel.prefab 到 Resources/UI/Boot 下,供 UIMgr/ResMgr 按路径加载。
/// 程序化构建:Unity 自动解析脚本 GUID / TMP 字体,脚本字段引用在此接好,避免手写 .prefab YAML。
///
/// 复用项目通用按钮 <c>Resources/UI/Common/CommonButton.prefab</c>(挂 YOTOButton,带悬停/点击缩放),
/// 保持与其它界面一致的按钮风格;四个按钮纵向居中排列:新游戏 / 读取存档 / 设置 / 退出游戏。
///
/// 菜单:Tools/UI/Build StartPanel Prefab
/// </summary>
public static class StartPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Boot";
    private const string PrefabPath = Dir + "/StartPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private const float ButtonGap = 130f; // 按钮纵向间距(CommonButton 高约 99)

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build StartPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (btnPrefab == null)
        {
            Debug.LogError($"[StartPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(全屏 + 背景 + CanvasGroup + YOTOUIShow + StartPanel)----------
        var root = NewUI("StartPanel", out var rootRt);
        Stretch(rootRt);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.11f, 0.14f, 1f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -160);
        titleRt.sizeDelta = new Vector2(900, 160);
        NewText(titleGo, "YFramework", 90, TextAlignmentOptions.Center);

        // ---------- 四个按钮(纵向居中)----------
        // 四个按钮整体围绕中心对称排布:上 1.5/0.5 格、下 -0.5/-1.5 格
        var btnNew = BuildButton(btnPrefab, "Btn_New", "新游戏", root.transform, ButtonGap * 1.5f);
        var btnContinue = BuildButton(btnPrefab, "Btn_Continue", "读取存档", root.transform, ButtonGap * 0.5f);
        var btnSetting = BuildButton(btnPrefab, "Btn_Setting", "设置", root.transform, ButtonGap * -0.5f);
        var btnQuit = BuildButton(btnPrefab, "Btn_Quit", "退出游戏", root.transform, ButtonGap * -1.5f);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<StartPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.StartPanel;
        panel.btn_new = btnNew;
        panel.btn_continue = btnContinue;
        panel.btn_setting = btnSetting;
        panel.btn_quit = btnQuit;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StartPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    /// <summary>实例化通用按钮预制体,设置名称/文本/居中位置,返回其 Button(YOTOButton)。</summary>
    private static Button BuildButton(GameObject prefab, string name, string label, Transform parent, float y)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.SetActive(true);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);

        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;

        return go.GetComponent<Button>(); // YOTOButton : Button
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
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
