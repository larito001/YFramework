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
    private const string CommonButtonPath = "Assets/Resources/UI/Common/CommonButton.prefab"; // 购买按钮用绿色通用按钮
    private const string InfoIconPath = "Assets/Art/UI/NewUI/Shared/Icons/PictoIcon/256/info_round.png"; // 明细「i」图标
    // 卡面边框(与任务卡同款:白底九宫格 + 浅内边 + 黑描边)。整卡底色按品质运行时染(见 ShopPanel)。
    private const string FrameDir = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Frame/CardFrame/";
    private const string FrameBg = FrameDir + "CardFrame_04_White_Bg.png";
    private const string FrameInnerBorder = FrameDir + "CardFrame_04_White_InnerBorder.png";
    private const string FrameBorder = FrameDir + "CardFrame_04_White_Border.png";
    private static readonly Color CardBg = new Color(0.97f, 0.97f, 0.95f, 1f);   // 卡底:白(与任务卡一致)
    private static readonly Color CardText = new Color(0.13f, 0.14f, 0.22f, 1f); // 名称等:深色(衬白卡底)
    private static readonly Color PriceColor = new Color(0.62f, 0.45f, 0.10f, 1f); // 价格数字:暗金
    private static readonly Color InnerBorderColor = new Color(0.69f, 0.886f, 1f, 1f); // 浅蓝内边(与任务卡一致)
    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build ShopCard Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 根:白色九宫格卡底 + 整卡点击 Button + View(整卡白底,品质色只染道具图背景)
        var root = NewUI("ShopCard", out var rootRt);
        rootRt.sizeDelta = new Vector2(550, 560);
        var bg = root.AddComponent<Image>();
        bg.sprite = LoadSprite(FrameBg);
        bg.type = Image.Type.Sliced;
        bg.color = CardBg; // 白卡底
        var view = root.AddComponent<ShopCardView>();
        var cardBtn = root.AddComponent<Button>();
        cardBtn.transition = Selectable.Transition.None; // 不改卡底色
        cardBtn.targetGraphic = bg;

        // 卡面边框(浅蓝内边 + 黑描边,与任务卡同款;铺满整卡,不挡点击)
        var innerImg = Skin("InnerBorder", root.transform, FrameInnerBorder, InnerBorderColor);
        innerImg.raycastTarget = false;
        var borderImg = Skin("Border", root.transform, FrameBorder, Color.black);
        borderImg.raycastTarget = false;

        // 品质框(只罩道具图区域,按品质染色;道具快照背景透明,这层即「武器的背景」)
        var qFrame = NewUI("QualityFrame", out var qRt, root.transform);
        qRt.anchorMin = Vector2.zero; qRt.anchorMax = Vector2.one;
        qRt.offsetMin = new Vector2(20, 270); qRt.offsetMax = new Vector2(-20, -20);
        var qImg = qFrame.AddComponent<Image>();
        qImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ItemQualityPalette.FrameSpritePath);
        qImg.type = Image.Type.Sliced; qImg.raycastTarget = false;

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
        var nameTmp = NewText(name, "名称", 30, TextAlignmentOptions.Center, CardText);

        // 价格(币种图标 + 数字;图标按 PriceType 运行时动态加载,替代原「金币」文字)
        var price = NewUI("Price", out var priceRt, root.transform);
        priceRt.anchorMin = new Vector2(0, 0); priceRt.anchorMax = new Vector2(1, 0); priceRt.pivot = new Vector2(0.5f, 0);
        priceRt.offsetMin = new Vector2(6, 130); priceRt.offsetMax = new Vector2(-6, 190);
        var priceHlg = price.AddComponent<HorizontalLayoutGroup>();
        priceHlg.childAlignment = TextAnchor.MiddleCenter; priceHlg.spacing = 8;
        priceHlg.childControlWidth = true; priceHlg.childControlHeight = true;
        priceHlg.childForceExpandWidth = false; priceHlg.childForceExpandHeight = false;
        var priceIconGo = NewUI("Icon", out _, price.transform);
        var priceIconLe = priceIconGo.AddComponent<LayoutElement>();
        priceIconLe.minWidth = priceIconLe.preferredWidth = 48; priceIconLe.minHeight = priceIconLe.preferredHeight = 48;
        var priceIconImg = priceIconGo.AddComponent<Image>();
        priceIconImg.preserveAspect = true; priceIconImg.raycastTarget = false;
        var priceValueGo = NewUI("Value", out _, price.transform);
        var priceTmp = NewText(priceValueGo, "0", 28, TextAlignmentOptions.MidlineLeft, PriceColor);

        // 购买按钮(底部):绿色通用按钮 CommonButton;买得起/买不起/已拥有的状态色由 ShopPanel 染其 Bg 子物体。
        var buyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommonButtonPath);
        var buyGo = (GameObject)PrefabUtility.InstantiatePrefab(buyPrefab, root.transform);
        buyGo.name = "Buy";
        ((RectTransform)buyGo.transform).localScale = Vector3.one; // 复位 art-kit 烤的 3 倍缩放
        var buyRt = (RectTransform)buyGo.transform;
        buyRt.anchorMin = new Vector2(0, 0); buyRt.anchorMax = new Vector2(1, 0); buyRt.pivot = new Vector2(0.5f, 0);
        buyRt.offsetMin = new Vector2(20, 20); buyRt.offsetMax = new Vector2(-20, 115);
        var buyBtn = buyGo.GetComponent<Button>();
        var buyBgTf = buyGo.transform.Find("Bg");
        var buyImg = buyBgTf != null ? buyBgTf.GetComponent<Image>() : buyGo.GetComponent<Image>(); // 状态色染这块可见底
        var buyLabelTmp = buyGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buyLabelTmp != null) { buyLabelTmp.text = "购买"; buyLabelTmp.fontSize = UITheme.Font(28); buyLabelTmp.color = Color.white; }

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
        infoRt.sizeDelta = new Vector2(112f, 112f); // 放大一倍
        var infoImg = infoGo.AddComponent<Image>();
        infoImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(InfoIconPath); // 圆形「i」按钮图(美术 info_round)
        infoImg.color = new Color(0.15f, 0.45f, 0.85f, 1f); // 原来的蓝色(图标不变,只染蓝)
        infoImg.preserveAspect = true;
        infoImg.raycastTarget = true;
        var infoBadge = infoGo.AddComponent<ItemIconDescButton>();
        infoGo.transform.SetAsLastSibling(); // 盖在卡片内容之上,确保点得到

        // 接 View 字段
        view.bg = bg;
        view.cardButton = cardBtn;
        view.qualityFrame = qImg; // 品质色只染道具图背景(整卡白底)
        view.pic = picImg;
        view.nameText = nameTmp;
        view.priceIcon = priceIconImg;
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

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[ShopCardBuilder] 找不到精灵 {path}");
        return s;
    }

    /// <summary>铺满父级的九宫格皮肤图(用于卡面边框层)。</summary>
    private static Image Skin(string name, Transform parent, string spritePath, Color color)
    {
        var go = NewUI(name, out var rt, parent);
        Stretch(rt);
        var img = go.AddComponent<Image>();
        img.sprite = LoadSprite(spritePath);
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

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
