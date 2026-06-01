#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成主界面(大厅)预制体 StartPanel.prefab 到 Resources/UI/Boot 下,供 UIMgr/ResMgr 按路径加载。
/// 程序化构建:Unity 自动解析脚本 GUID / TMP 字体,脚本字段引用在此接好,避免手写 .prefab YAML。
///
/// 竖屏手机布局:
///   左上:设置(方形按钮)            右上:体力 + 金币两项资源,体力后带「广告补充」小按钮
///   底部:左侧竖排 任务/商店 · 中间 准备(主按钮,加大) · 右侧 图鉴
///
/// 按钮复用项目通用按钮 <c>Resources/UI/Common/CommonButton.prefab</c>(挂 YOTOButton,带悬停/点击缩放),
/// 与其它界面保持一致的按钮风格。
///
/// 菜单:Tools/UI/Build StartPanel Prefab
/// </summary>
public static class StartPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Boot";
    private const string PrefabPath = Dir + "/StartPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

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

        // 尺寸按"宽度恒为 1920 单位"的画布(CanvasScaler match=width,refWidth=1920)来定;
        // 竖屏时画布高约 4000+ 单位,故元素普遍偏大才不至于在手机上显得很小。

        // ---------- 左上:设置(方形按钮)----------
        var btnSetting = BuildButton(btnPrefab, "Btn_Setting", "设置", root.transform, 48);
        TopLeft((RectTransform)btnSetting.transform, new Vector2(60, -80), new Vector2(180, 180));

        // ---------- 右上:体力 + 金币(体力后跟「广告补充」小按钮)----------
        // 行 1(上):体力胶囊 + 紧随其右的广告「+」按钮;行 2(下):金币胶囊。均以右上角为原点向左排。
        const float pillH = 100f, adBtn = 100f, gap = 16f;

        // 广告「+」按钮贴右上角(在体力之后/之右,预留接广告)
        var btnEnergyAd = BuildButton(btnPrefab, "Btn_EnergyAd", "+", root.transform, 60);
        TopRight((RectTransform)btnEnergyAd.transform, new Vector2(-60, -80), new Vector2(adBtn, adBtn));

        // 体力胶囊:在广告按钮左侧
        var energyText = BuildPill(root.transform, "Energy", "体力 50",
            new Vector2(-(60 + adBtn + gap), -80), new Vector2(300, pillH));

        // 金币胶囊:体力下一行
        var goldText = BuildPill(root.transform, "Gold", "金币 0",
            new Vector2(-60, -(80 + pillH + gap)), new Vector2(360, pillH));

        // ---------- 底部:左竖排(任务/商店) · 中(准备) · 右(图鉴)----------
        const float sideBtn = 220f, sideGap = 36f, bottomY = 150f;

        // 左侧竖排容器:任务(上)/ 商店(下),VerticalLayoutGroup 自上而下
        var leftCol = NewUI("LeftColumn", out var leftRt, root.transform);
        leftRt.anchorMin = leftRt.anchorMax = new Vector2(0, 0);
        leftRt.pivot = new Vector2(0, 0);
        leftRt.anchoredPosition = new Vector2(60, bottomY);
        leftRt.sizeDelta = new Vector2(sideBtn, sideBtn * 2 + sideGap);
        var vlg = leftCol.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = sideGap;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = true;
        var btnTask = BuildButton(btnPrefab, "Btn_Task", "任务", leftCol.transform, 56);
        var btnShop = BuildButton(btnPrefab, "Btn_Shop", "商店", leftCol.transform, 56);

        // 中间主按钮:准备(加大,作为大厅主操作)
        var btnPrepare = BuildButton(btnPrefab, "Btn_Prepare", "准备", root.transform, 76);
        var prepareRt = (RectTransform)btnPrepare.transform;
        prepareRt.anchorMin = prepareRt.anchorMax = new Vector2(0.5f, 0);
        prepareRt.pivot = new Vector2(0.5f, 0);
        prepareRt.anchoredPosition = new Vector2(0, bottomY + 20);
        prepareRt.sizeDelta = new Vector2(560, 280);

        // 右侧:图鉴(竖向居中对齐左侧竖排)
        var btnCodex = BuildButton(btnPrefab, "Btn_Codex", "图鉴", root.transform, 56);
        var codexRt = (RectTransform)btnCodex.transform;
        codexRt.anchorMin = codexRt.anchorMax = new Vector2(1, 0);
        codexRt.pivot = new Vector2(1, 0);
        codexRt.anchoredPosition = new Vector2(-60, bottomY + (sideBtn * 2 + sideGap - sideBtn) / 2f);
        codexRt.sizeDelta = new Vector2(sideBtn, sideBtn);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<StartPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.StartPanel;
        panel.energyText = energyText;
        panel.goldText = goldText;
        panel.btn_setting = btnSetting;
        panel.btn_energyAd = btnEnergyAd;
        panel.btn_shop = btnShop;
        panel.btn_task = btnTask;
        panel.btn_prepare = btnPrepare;
        panel.btn_codex = btnCodex;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StartPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    /// <summary>实例化通用按钮预制体,设置名称/文本/字号,返回其 Button(YOTOButton)。位置/尺寸由调用方或布局组决定。</summary>
    private static Button BuildButton(GameObject prefab, string name, string label, Transform parent, float fontSize)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.SetActive(true);

        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = UITheme.Font(fontSize); // 通用按钮默认 24pt,在 1920 宽画布上偏小
        }

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

    /// <summary>锚定到左上角:anchoredPosition 以左上为原点(向右为 +x,向下为 -y)。</summary>
    private static void TopLeft(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    /// <summary>锚定到右上角:anchoredPosition 以右上为原点(向左为 -x,向下为 -y)。</summary>
    private static void TopRight(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    /// <summary>右上角资源「胶囊」:深色圆角底 + 右对齐文本,返回文本(数值由 StartPanel 运行时刷新)。</summary>
    private static TextMeshProUGUI BuildPill(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size)
    {
        var go = NewUI(name, out var rt, parent);
        TopRight(rt, anchoredPos, size);
        var bg = go.AddComponent<Image>();
        bg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.25f, 0.27f, 0.33f, 1f);
        var tmp = NewChildText(go, "Text", text, 48, TextAlignmentOptions.Right);
        ((RectTransform)tmp.transform).offsetMax = new Vector2(-28, 0); // 右侧留点边距
        ((RectTransform)tmp.transform).offsetMin = new Vector2(20, 0);  // 左侧留点边距
        return tmp;
    }

    /// <summary>在父物体内铺满一个 TMP 文本子物体,返回它(用于给 Image 容器加文字标签)。</summary>
    private static TextMeshProUGUI NewChildText(GameObject parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = NewUI(name, out var rt, parent.transform);
        Stretch(rt);
        return NewText(go, text, size, align);
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

    /// <summary>加载 Unity 内置 UI 精灵(Knob / UISprite 等),失败返回 null(Image 退化为纯色块)。</summary>
    private static Sprite BuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }
}
#endif
