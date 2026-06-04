#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成商店卡片预制体 ShopCard.prefab 到 Resources/UI/Shop 下。<see cref="ShopPanel"/> 运行时 instantiate 它、
/// 取 <see cref="ShopCardView"/> 绑定数据,替代旧的 NewChild/NewText 运行时拼节点。
/// 卡片长相(图/名/价/购买按钮/选中框)在这里改,改完点菜单重建即可,无需动面板代码。
/// 尺寸按 GridLayoutGroup cellSize 550×560(实际由网格控制)。菜单:Tools/UI/Build ShopCard Prefab
/// </summary>
public static class ShopCardBuilder
{
    private const string Dir = "Assets/Resources/UI/Shop";
    private const string PrefabPath = Dir + "/ShopCard.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build ShopCard Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 根:卡底 + 整卡点击 Button + View
        var root = NewUI("ShopCard", out var rootRt);
        rootRt.sizeDelta = new Vector2(550, 560);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.20f, 0.25f, 1f);
        var view = root.AddComponent<ShopCardView>();
        var cardBtn = root.AddComponent<Button>();
        cardBtn.transition = Selectable.Transition.None; // 不改卡底色
        cardBtn.targetGraphic = bg;

        // 图片(上部,占大半):preserveAspect,默认隐藏(绑定时按有无图启用)
        var pic = NewUI("Pic", out var picRt, root.transform);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(20, 270); picRt.offsetMax = new Vector2(-20, -20);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true; picImg.enabled = false;

        // 名称
        var name = NewUI("Name", out var nameRt, root.transform);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 0); nameRt.pivot = new Vector2(0.5f, 0);
        nameRt.offsetMin = new Vector2(6, 196); nameRt.offsetMax = new Vector2(-6, 266);
        var nameTmp = NewText(name, "名称", 30, TextAlignmentOptions.Center, Color.white);

        // 价格
        var price = NewUI("Price", out var priceRt, root.transform);
        priceRt.anchorMin = new Vector2(0, 0); priceRt.anchorMax = new Vector2(1, 0); priceRt.pivot = new Vector2(0.5f, 0);
        priceRt.offsetMin = new Vector2(6, 130); priceRt.offsetMax = new Vector2(-6, 190);
        var priceTmp = NewText(price, "价格", 26, TextAlignmentOptions.Center, new Color(1f, 0.83f, 0.47f, 1f));

        // 购买按钮(底部)
        var buy = NewUI("Buy", out var buyRt, root.transform);
        buyRt.anchorMin = new Vector2(0, 0); buyRt.anchorMax = new Vector2(1, 0); buyRt.pivot = new Vector2(0.5f, 0);
        buyRt.offsetMin = new Vector2(20, 20); buyRt.offsetMax = new Vector2(-20, 115);
        var buyImg = buy.AddComponent<Image>();
        buyImg.color = new Color(0.30f, 0.78f, 0.36f, 1f);
        var buyBtn = buy.AddComponent<Button>();
        buyBtn.targetGraphic = buyImg;
        var label = NewUI("Label", out var labelRt, buy.transform);
        Stretch(labelRt);
        var buyLabelTmp = NewText(label, "购买", 28, TextAlignmentOptions.Center, Color.white);

        // 金色选中框(4 条细边拼框,预置好默认隐藏;选中时显隐即可)
        var frameGo = NewUI("SelectFrame", out var frameRt, root.transform);
        Stretch(frameRt);
        var gold = new Color(1f, 0.85f, 0.2f, 1f);
        AddEdge(frameRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), new Vector2(0, 0), gold); // 上
        AddEdge(frameRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 8), gold);  // 下
        AddEdge(frameRt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(8, 0), gold);  // 左
        AddEdge(frameRt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-8, 0), new Vector2(0, 0), gold); // 右
        frameGo.SetActive(false);

        // 右上角「i」描述按钮(挂 ItemIconDescButton;运行时只填 itemId,不再运行时构建)
        var infoGo = NewUI("InfoBadge", out var infoRt, root.transform);
        infoRt.anchorMin = infoRt.anchorMax = infoRt.pivot = new Vector2(1f, 1f); // 右上角
        infoRt.anchoredPosition = new Vector2(-6f, -6f);
        infoRt.sizeDelta = new Vector2(56f, 56f);
        var infoImg = infoGo.AddComponent<Image>();
        infoImg.color = new Color(0.15f, 0.45f, 0.85f, 0.95f); // 蓝色徽标
        infoImg.raycastTarget = true;
        var infoBadge = infoGo.AddComponent<ItemIconDescButton>();
        var infoLabel = NewUI("i", out var infoLabelRt, infoGo.transform);
        Stretch(infoLabelRt);
        var infoTmp = NewText(infoLabel, "i", 34, TextAlignmentOptions.Center, Color.white);
        infoTmp.fontStyle = FontStyles.Bold;
        infoGo.transform.SetAsLastSibling(); // 盖在卡片内容之上,确保点得到

        // 接 View 字段
        view.bg = bg;
        view.cardButton = cardBtn;
        view.pic = picImg;
        view.nameText = nameTmp;
        view.priceText = priceTmp;
        view.buyBg = buyImg;
        view.buyButton = buyBtn;
        view.buyLabel = buyLabelTmp;
        view.selectFrame = frameGo;
        view.infoBadge = infoBadge;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ShopCardBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static void AddEdge(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, Color color)
    {
        var go = NewUI("Edge", out var rt, parent);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
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

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align, Color color)
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
