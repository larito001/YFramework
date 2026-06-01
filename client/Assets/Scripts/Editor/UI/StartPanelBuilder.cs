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
/// 竖屏手机布局(对应设计稿):
///   左上:头像框 + 等级        右上:资源/金币
///   左侧:设置(方形按钮)      底部一排:商店 / 准备 / 图鉴 / 出发
///
/// 五个按钮复用项目通用按钮 <c>Resources/UI/Common/CommonButton.prefab</c>(挂 YOTOButton,带悬停/点击缩放),
/// 与其它界面保持一致的按钮风格。底部四按钮用 HorizontalLayoutGroup 均分,自适应屏宽。
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

        // ---------- 左上:头像框 + 等级 ----------
        var avatarGo = NewUI("AvatarFrame", out var avatarRt, root.transform);
        TopLeft(avatarRt, new Vector2(60, -80), new Vector2(220, 220));
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.sprite = BuiltinSprite("UI/Skin/Knob.psd");
        avatarImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);

        var levelGo = NewUI("Level", out var levelRt, root.transform);
        TopLeft(levelRt, new Vector2(60, -320), new Vector2(220, 80));
        var levelBg = levelGo.AddComponent<Image>();
        levelBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        levelBg.type = Image.Type.Sliced;
        levelBg.color = new Color(0.25f, 0.27f, 0.33f, 1f);
        var levelText = NewChildText(levelGo, "等级", "Lv.1", 44, TextAlignmentOptions.Center);

        // ---------- 右上:资源/金币 ----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(1, 1);
        coinRt.pivot = new Vector2(1, 1);
        coinRt.anchoredPosition = new Vector2(-60, -80);
        coinRt.sizeDelta = new Vector2(360, 110);
        var coinBg = coinGo.AddComponent<Image>();
        coinBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        coinBg.type = Image.Type.Sliced;
        coinBg.color = new Color(0.25f, 0.27f, 0.33f, 1f);
        var coinText = NewChildText(coinGo, "资源金币", "0", 48, TextAlignmentOptions.Right);
        ((RectTransform)coinText.transform).offsetMax = new Vector2(-28, 0); // 右侧留点边距

        // ---------- 左侧:设置(方形按钮)----------
        var btnSetting = BuildButton(btnPrefab, "Btn_Setting", "设置", root.transform, 52);
        var settingRt = (RectTransform)btnSetting.transform;
        TopLeft(settingRt, new Vector2(60, -480), new Vector2(200, 200));

        // ---------- 底部一排:商店 / 任务 / 准备 / 图鉴(HorizontalLayoutGroup 均分)----------
        var bar = NewUI("BottomBar", out var barRt, root.transform);
        barRt.anchorMin = new Vector2(0, 0);
        barRt.anchorMax = new Vector2(1, 0);
        barRt.pivot = new Vector2(0.5f, 0);
        barRt.offsetMin = new Vector2(60, 120);  // 左/下边距(下方留安全区)
        barRt.offsetMax = new Vector2(-60, 420); // 右边距 + 高度 300
        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var btnShop = BuildButton(btnPrefab, "Btn_Shop", "商店", bar.transform, 56);
        var btnTask = BuildButton(btnPrefab, "Btn_Task", "任务", bar.transform, 56);
        var btnPrepare = BuildButton(btnPrefab, "Btn_Prepare", "准备", bar.transform, 56); // 居中
        var btnCodex = BuildButton(btnPrefab, "Btn_Codex", "图鉴", bar.transform, 56);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<StartPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.StartPanel;
        panel.avatarFrame = avatarImg;
        panel.levelText = levelText;
        panel.coinText = coinText;
        panel.btn_setting = btnSetting;
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
            text.fontSize = fontSize; // 通用按钮默认 24pt,在 1920 宽画布上偏小
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
        tmp.fontSize = size;
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
