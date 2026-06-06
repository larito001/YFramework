#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成登录界面预制体 LoginPanel.prefab 到 Resources/UI/Login 下,供 UIMgr/ResMgr 按路径加载。
/// 程序化构建(Unity 自动解析脚本 GUID / TMP 字体),脚本字段引用在此接好,避免手写 .prefab YAML。
///
/// 竖屏手机布局(参考 TapTap 登录界面设计规范):
///   左上:版本号(运行时填 Application.version)   右上:适龄提示徽标(静态 12+)
///   中上:游戏标题(用 PlayerSettings.productName)
///   底部:「TapTap 登录」主按钮(复用通用按钮)+ 上方状态提示 + 最底部版号/出版信息占位
///
/// 接其它登录模块时:在底部按钮处复制一份、改 name/文案,并在 LoginPanel 里接好对应字段与点击即可。
///
/// 菜单:Tools/UI/Build LoginPanel Prefab
/// </summary>
public static class LoginPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Login";
    private const string PrefabPath = Dir + "/LoginPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private const string TapTapButtonSpritePath = "Assets/Art/UI/taptapButton.png";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build LoginPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (btnPrefab == null)
        {
            Debug.LogError($"[LoginPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(全屏 + 背景 + CanvasGroup + YOTOUIShow + LoginPanel)----------
        var root = NewUI("LoginPanel", out var rootRt);
        Stretch(rootRt);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.18f, 1f); // 深色底(接入正式美术背景图后可替换 sprite)
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 中上:游戏标题(用产品名,程序化烘焙便于改名后重建)----------
        var title = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.62f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = Vector2.zero;
        titleRt.sizeDelta = new Vector2(1700, 400);
        NewText(title, PlayerSettings.productName, 140, TextAlignmentOptions.Center);

        // ---------- 左上:版本号(运行时填 Application.version,这里占位)----------
        var ver = NewUI("Version", out var verRt, root.transform);
        TopLeft(verRt, new Vector2(40, -40), new Vector2(600, 60));
        var versionText = NewText(ver, "v1.0.0", 36, TextAlignmentOptions.TopLeft);
        versionText.color = new Color(1f, 1f, 1f, 0.7f);

        // ---------- 右上:适龄提示徽标(静态 12+,深色圆底)----------
        var age = NewUI("AgeRating", out var ageRt, root.transform);
        TopRight(ageRt, new Vector2(-40, -40), new Vector2(130, 130));
        var ageBg = age.AddComponent<Image>();
        ageBg.color = new Color(0f, 0f, 0f, 0.45f);
        NewChildText(age, "Text", "12+", 50, TextAlignmentOptions.Center);

        // ---------- 底部:状态提示(登录失败/取消时显示,在按钮上方)----------
        var status = NewUI("Status", out var statusRt, root.transform);
        statusRt.anchorMin = statusRt.anchorMax = new Vector2(0.5f, 0);
        statusRt.pivot = new Vector2(0.5f, 0);
        statusRt.anchoredPosition = new Vector2(0, 780);
        statusRt.sizeDelta = new Vector2(1000, 70);
        var statusText = NewText(status, string.Empty, 38, TextAlignmentOptions.Center);
        statusText.color = new Color(1f, 0.5f, 0.45f, 1f);

        // ---------- 底部:TapTap 登录主按钮(复用通用按钮)----------
        var btnLogin = UIButtonFactory.Build(btnPrefab, "Btn_TapTapLogin", "TapTap 登录", root.transform, fontSize: 60);
        var loginRt = (RectTransform)btnLogin.transform;
        loginRt.anchorMin = loginRt.anchorMax = new Vector2(0.5f, 0);
        loginRt.pivot = new Vector2(0.5f, 0);
        loginRt.anchoredPosition = new Vector2(0, 540);
        loginRt.sizeDelta = new Vector2(760, 200);
        var loginLabel = btnLogin.GetComponentInChildren<TextMeshProUGUI>(true);
        // 用美术整图 taptapButton 替换通用按钮皮肤:整张图已含「TapTap 登录」文字与底色,
        // 故把底图 Bg 换成该 Sprite、隐藏绿色内描边与 TMP 文字,避免与整图重叠。
        ApplyTapTapSkin(btnLogin.gameObject, loginLabel);

        // ---------- 最底部:版号 / 出版信息(合规占位,上线前补全真实信息)----------
        var copy = NewUI("Copyright", out var copyRt, root.transform);
        copyRt.anchorMin = copyRt.anchorMax = new Vector2(0.5f, 0);
        copyRt.pivot = new Vector2(0.5f, 0);
        copyRt.anchoredPosition = new Vector2(0, 150);
        copyRt.sizeDelta = new Vector2(1750, 200);
        var copyText = NewText(copy,
            "健康游戏忠告:抵制不良游戏,谨防受骗上当。适度游戏益脑,沉迷游戏伤身。合理安排时间,享受健康生活。\n著作权人 / 出版物号(ISBN) / 出版单位(上线前补全)",
            30, TextAlignmentOptions.Center);
        copyText.color = new Color(1f, 1f, 1f, 0.6f);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<LoginPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.LoginPanel;
        panel.btn_login = btnLogin;
        panel.loginLabel = loginLabel;
        panel.versionText = versionText;
        panel.statusText = statusText;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LoginPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    /// <summary>
    /// 把通用按钮换成美术整图 taptapButton:底图 Bg 用该 Sprite(白色、Simple、保持比例),
    /// 隐藏 InnerBorder1 绿色描边与 TMP 文字(整图已自带文字),让按钮外观就是这张图。
    /// </summary>
    private static void ApplyTapTapSkin(GameObject button, TextMeshProUGUI label)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TapTapButtonSpritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[LoginPanelBuilder] 未找到 {TapTapButtonSpritePath},沿用通用按钮皮肤。");
            return;
        }

        var bg = button.transform.Find("Bg");
        if (bg != null && bg.TryGetComponent<Image>(out var bgImg))
        {
            bgImg.sprite = sprite;
            bgImg.color = Color.white;          // 还原整图本色(通用按钮原本染成绿色)
            bgImg.type = Image.Type.Simple;     // 整图非九宫格
            bgImg.preserveAspect = true;        // 不拉伸变形
        }

        var border = button.transform.Find("InnerBorder1");
        if (border != null) border.gameObject.SetActive(false);

        if (label != null) label.gameObject.SetActive(false); // 文字已烤进整图
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
}
#endif
