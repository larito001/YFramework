#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成装备卡片预制体 EquipCard.prefab 到 Resources/UI/Equip 下。<see cref="EquipPanel"/> 运行时 instantiate 它、
/// 取 <see cref="EquipCardView"/> 绑定数据。卡片长相(图/名/选中描边)在这里改,改完点菜单重建。
/// 尺寸 320×340(LayoutElement,横向行布局)。菜单:Tools/UI/Build EquipCard Prefab
/// </summary>
public static class EquipCardBuilder
{
    private const string Dir = "Assets/Resources/UI/Equip";
    private const string PrefabPath = Dir + "/EquipCard.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build EquipCard Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 根:卡底 + 选中按钮 + 选中描边(默认关) + 尺寸 + View
        var root = NewUI("EquipCard", out var rootRt);
        rootRt.sizeDelta = new Vector2(320, 340);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.30f, 0.78f, 0.36f, 1f); // 默认绿,绑定时按 owned 改
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        var outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        outline.effectDistance = new Vector2(6, 6);
        outline.enabled = false; // 选中时启用
        var le = root.AddComponent<LayoutElement>();
        le.preferredWidth = 320; le.preferredHeight = 340;
        var view = root.AddComponent<EquipCardView>();

        // 图片(上部):preserveAspect,默认隐藏(绑定按有无图启用)
        var pic = NewUI("Pic", out var picRt, root.transform);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(16, 90); picRt.offsetMax = new Vector2(-16, -16);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true; picImg.enabled = false;

        // 名称(底部)
        var name = NewUI("Name", out var nameRt, root.transform);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 0); nameRt.pivot = new Vector2(0.5f, 0);
        nameRt.offsetMin = new Vector2(6, 12); nameRt.offsetMax = new Vector2(-6, 78);
        var nameTmp = NewText(name, "名称", 32, TextAlignmentOptions.Center, Color.white);

        view.bg = bg; view.button = btn; view.outline = outline; view.pic = picImg; view.nameText = nameTmp; view.layout = le;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipCardBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
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
