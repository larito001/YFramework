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
    private const string MaskMatPath = "Assets/Art/UI/ScopeMask.mat"; // 瞄准镜黑边遮罩材质(用 Hunting/ScopeMask shader)

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

        // ---------- 瞄准镜黑边遮罩(最底层:HUD 控件都画在它之上,瞄准时仍可见可点)----------
        var scopeMask = BuildScopeMask(root.transform);

        // ---------- 左上:地图名 + 本局积分 ----------
        var mapGo = NewUI("MapName", out var mapRt, root.transform);
        mapRt.anchorMin = new Vector2(0, 1); mapRt.anchorMax = new Vector2(0, 1); mapRt.pivot = new Vector2(0, 1);
        mapRt.anchoredPosition = new Vector2(40, -40); mapRt.sizeDelta = new Vector2(700, 90);
        var mapText = NewText(mapGo, "当前地图名称", 52, TextAlignmentOptions.Left);

        var scoreGo = NewUI("Score", out var scoreRt, root.transform);
        scoreRt.anchorMin = new Vector2(0, 1); scoreRt.anchorMax = new Vector2(0, 1); scoreRt.pivot = new Vector2(0, 1);
        scoreRt.anchoredPosition = new Vector2(40, -150); scoreRt.sizeDelta = new Vector2(800, 60);
        var scoreText = NewText(scoreGo, "当前对局获得积分：0", 36, TextAlignmentOptions.Left);

        // 对局 HUD 不显示金币/体力(资源只在大厅各面板展示)

        // ---------- 中部:瞄准镜准星(默认隐藏)----------
        var scope = BuildScope(root.transform);

        // ---------- 底部中间:瞄准/射击 圆钮(同一个钮,文字随状态切换)----------
        var actionBtn = BuildRoundButton("Btn_Action", "瞄准", root.transform,
            new Vector2(0.5f, 0), new Vector2(0, 240), 320, out var actionLabel);

        // ---------- 底部中:剩余子弹 ----------
        var ammoGo = NewUI("Ammo", out var ammoRt, root.transform);
        ammoRt.anchorMin = new Vector2(0.5f, 0); ammoRt.anchorMax = new Vector2(0.5f, 0); ammoRt.pivot = new Vector2(0.5f, 0);
        ammoRt.anchoredPosition = new Vector2(0, 40); ammoRt.sizeDelta = new Vector2(500, 70);
        var ammoText = NewText(ammoGo, "剩余子弹：0", 40, TextAlignmentOptions.Center);

        // ---------- 左下:结束打猎 ----------
        var endGo = NewUI("Btn_End", out var endRt, root.transform);
        endRt.anchorMin = endRt.anchorMax = new Vector2(0, 0); endRt.pivot = new Vector2(0, 0);
        endRt.anchoredPosition = new Vector2(40, 40); endRt.sizeDelta = new Vector2(300, 120);
        var endImg = endGo.AddComponent<Image>();
        endImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        endImg.type = Image.Type.Sliced;
        endImg.color = new Color(0.80f, 0.26f, 0.24f, 0.92f); // 红色:结束
        var endBtn = endGo.AddComponent<Button>();
        endBtn.targetGraphic = endImg;
        var endLbl = NewUI("Label", out var endLblRt, endGo.transform);
        Stretch(endLblRt);
        NewText(endLbl, "结束打猎", 44, TextAlignmentOptions.Center);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<GameMainPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.GameMainPanel;
        panel.mapNameText = mapText;
        panel.scoreText = scoreText;
        panel.scope = scope;
        panel.scopeMask = scopeMask;
        panel.actionBtn = actionBtn;
        panel.actionLabel = actionLabel;
        panel.ammoText = ammoText;
        panel.endBtn = endBtn;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GameMainPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 ============================

    /// <summary>
    /// 瞄准镜黑边遮罩:铺满屏幕的 RawImage,用 Hunting/ScopeMask shader 画成「中心圆透明、圆外黑」。
    /// 不挡射线(拖屏瞄准要能穿透),默认隐藏,瞄准时由脚本显示。
    /// </summary>
    private static GameObject BuildScopeMask(Transform parent)
    {
        var go = NewUI("ScopeMask", out var rt, parent);
        Stretch(rt);
        var raw = go.AddComponent<RawImage>();
        raw.color = Color.black;       // 黑边颜色实际由 shader 的 _Color 控制,这里兜底
        raw.raycastTarget = false;     // 不挡点击/拖拽
        raw.material = LoadOrCreateMaskMaterial();
        go.SetActive(false);
        return go;
    }

    /// <summary>取(没有则创建)瞄准镜遮罩材质。</summary>
    private static Material LoadOrCreateMaskMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaskMatPath);
        if (mat != null) return mat;

        var shader = Shader.Find("Hunting/ScopeMask");
        if (shader == null)
        {
            Debug.LogError("[GameMainPanelBuilder] 找不到 Hunting/ScopeMask shader,遮罩材质未创建");
            return null;
        }
        var dir = Path.GetDirectoryName(MaskMatPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MaskMatPath);
        return mat;
    }

    /// <summary>
    /// 瞄准镜准星:4 根带缺口的刻度线 + 中心小红点,**中心完全透空**——不再用填充方框/圆盘挡住瞄准点,
    /// 看得清要打的部位(头/心脏/身体)。返回准星根物体(默认隐藏,由脚本在瞄准时显示)。
    /// </summary>
    private static GameObject BuildScope(Transform parent)
    {
        var scope = NewUI("Scope", out var rt, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; // 居中:与射线(屏幕中心)对齐
        rt.sizeDelta = new Vector2(200, 200);

        const float gap = 22f;     // 中心留空半径(到刻度线内端)
        const float length = 46f;  // 每根刻度线长
        const float thick = 5f;    // 刻度线粗
        var lineColor = new Color(1f, 1f, 1f, 0.9f);
        float off = gap + length * 0.5f; // 刻度线中心到屏幕中心的距离

        // 上 / 下(竖向刻度)
        Tick(scope.transform, "TickUp",    new Vector2(thick, length), new Vector2(0,  off), lineColor);
        Tick(scope.transform, "TickDown",  new Vector2(thick, length), new Vector2(0, -off), lineColor);
        // 左 / 右(横向刻度)
        Tick(scope.transform, "TickLeft",  new Vector2(length, thick), new Vector2(-off, 0), lineColor);
        Tick(scope.transform, "TickRight", new Vector2(length, thick), new Vector2( off, 0), lineColor);

        // 中心小圆点(红色,标精确命中点,不挡视线)
        var dot = NewImage(scope.transform, "Dot", new Color(0.95f, 0.25f, 0.20f, 0.95f));
        dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // 圆点
        var dotRt = dot.rectTransform; dotRt.anchorMin = dotRt.anchorMax = dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = Vector2.zero; dotRt.sizeDelta = new Vector2(12, 12); dot.raycastTarget = false;

        scope.SetActive(false);
        return scope;
    }

    /// <summary>准星的一根刻度线(居中锚点 + 指定尺寸/偏移)。</summary>
    private static void Tick(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var img = NewImage(parent, name, color);
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size; r.anchoredPosition = pos;
        img.raycastTarget = false;
    }

    /// <summary>圆形按钮(内置 Knob 圆形精灵 + 居中文字),out 出文字组件供运行时切换文案。</summary>
    private static Button BuildRoundButton(string name, string label, Transform parent, Vector2 anchor, Vector2 pos, float size, out TextMeshProUGUI labelText)
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
        labelText = NewText(lblGo, label, 56, TextAlignmentOptions.Center);
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
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = Color.white; tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
