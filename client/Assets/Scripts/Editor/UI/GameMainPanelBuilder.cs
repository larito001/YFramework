#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成游戏内打猎 HUD 预制体 GameMainPanel.prefab(覆盖旧的)到 Resources/UI/Main 下。
/// 透明覆盖层(无遮罩,不挡场景):左上 地图名+积分、右上 资源金币、中部 瞄准镜准星、底部 瞄准/射击 圆钮 + 剩余子弹。
/// 逻辑由 <see cref="GameMainPanel"/> 接管。尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给。
///
/// 菜单:Tools/UI/Build GameMainPanel Prefab
/// </summary>
public static class GameMainPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Main";
    private const string PrefabPath = Dir + "/GameMainPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build GameMainPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // ---------- 根(透明覆盖,无 Image 遮罩)----------
        var root = NewUI("GameMainPanel", out var rootRt);
        Stretch(rootRt);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 左上:地图名 + 本局积分 ----------
        var mapGo = NewUI("MapName", out var mapRt, root.transform);
        mapRt.anchorMin = new Vector2(0, 1); mapRt.anchorMax = new Vector2(0, 1); mapRt.pivot = new Vector2(0, 1);
        mapRt.anchoredPosition = new Vector2(40, -40); mapRt.sizeDelta = new Vector2(700, 90);
        var mapText = NewText(mapGo, "当前地图名称", 52, TextAlignmentOptions.Left);

        var scoreGo = NewUI("Score", out var scoreRt, root.transform);
        scoreRt.anchorMin = new Vector2(0, 1); scoreRt.anchorMax = new Vector2(0, 1); scoreRt.pivot = new Vector2(0, 1);
        scoreRt.anchoredPosition = new Vector2(40, -150); scoreRt.sizeDelta = new Vector2(800, 60);
        var scoreText = NewText(scoreGo, "当前对局获得积分：0", 36, TextAlignmentOptions.Left);

        // ---------- 右上:资源金币 ----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = new Vector2(1, 1); coinRt.anchorMax = new Vector2(1, 1); coinRt.pivot = new Vector2(1, 1);
        coinRt.anchoredPosition = new Vector2(-40, -40); coinRt.sizeDelta = new Vector2(380, 150);
        var coinText = NewText(coinGo, "金币 0\n钻石 0", 40, TextAlignmentOptions.TopRight);

        // ---------- 中部:瞄准镜准星(默认隐藏)----------
        var scope = BuildScope(root.transform);

        // ---------- 底部:瞄准 / 射击 圆钮 ----------
        var aimBtn = BuildRoundButton("Btn_Aim", "瞄准", root.transform,
            new Vector2(0, 0), new Vector2(140, 180), 320);
        var shootBtn = BuildRoundButton("Btn_Shoot", "射击", root.transform,
            new Vector2(1, 0), new Vector2(-140, 180), 320);

        // ---------- 底部中:剩余子弹 ----------
        var ammoGo = NewUI("Ammo", out var ammoRt, root.transform);
        ammoRt.anchorMin = new Vector2(0.5f, 0); ammoRt.anchorMax = new Vector2(0.5f, 0); ammoRt.pivot = new Vector2(0.5f, 0);
        ammoRt.anchoredPosition = new Vector2(0, 40); ammoRt.sizeDelta = new Vector2(500, 70);
        var ammoText = NewText(ammoGo, "剩余子弹：0", 40, TextAlignmentOptions.Center);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<GameMainPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.GameMainPanel;
        panel.mapNameText = mapText;
        panel.scoreText = scoreText;
        panel.coinText = coinText;
        panel.scope = scope;
        panel.aimBtn = aimBtn;
        panel.shootBtn = shootBtn;
        panel.ammoText = ammoText;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GameMainPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 ============================

    /// <summary>瞄准镜准星:居中圆环 + 十字 + 中心方框。返回准星根物体(默认隐藏,由脚本在瞄准时显示)。</summary>
    private static GameObject BuildScope(Transform parent)
    {
        var scope = NewUI("Scope", out var rt, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 200); // 略偏上,避开底部按钮
        rt.sizeDelta = new Vector2(640, 640);

        // 圆环(用内置 Knob 填充圆 + 半透明,作为镜筒底色)
        var ring = NewImage(scope.transform, "Ring", new Color(0f, 0f, 0f, 0.25f));
        Stretch(ring.rectTransform);
        ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        ring.raycastTarget = false;

        // 十字线
        var hLine = NewImage(scope.transform, "CrossH", new Color(0, 0, 0, 0.8f));
        var hRt = hLine.rectTransform; hRt.anchorMin = new Vector2(0.05f, 0.5f); hRt.anchorMax = new Vector2(0.95f, 0.5f);
        hRt.offsetMin = new Vector2(0, -3); hRt.offsetMax = new Vector2(0, 3); hLine.raycastTarget = false;
        var vLine = NewImage(scope.transform, "CrossV", new Color(0, 0, 0, 0.8f));
        var vRt = vLine.rectTransform; vRt.anchorMin = new Vector2(0.5f, 0.05f); vRt.anchorMax = new Vector2(0.5f, 0.95f);
        vRt.offsetMin = new Vector2(-3, 0); vRt.offsetMax = new Vector2(3, 0); vLine.raycastTarget = false;

        // 中心方框(半透明,模拟锁定框)
        var box = NewImage(scope.transform, "Box", new Color(1f, 1f, 1f, 0.15f));
        var boxRt = box.rectTransform; boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(170, 170); box.raycastTarget = false;
        var ol = box.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(1, 1, 1, 0.6f); ol.effectDistance = new Vector2(3, 3);

        scope.SetActive(false);
        return scope;
    }

    /// <summary>圆形按钮(内置 Knob 圆形精灵 + 居中文字)。</summary>
    private static Button BuildRoundButton(string name, string label, Transform parent, Vector2 anchor, Vector2 pos, float size)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var lblGo = NewUI("Label", out var lblRt, go.transform);
        Stretch(lblRt);
        NewText(lblGo, label, 56, TextAlignmentOptions.Center);
        return btn;
    }

    // ============================ 工具 ============================

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = Color.white; tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
