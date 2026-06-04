using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 左键点击本元素弹出通用道具描述弹窗(<see cref="ItemDescPanel"/>)。
/// 商店/装备卡片用 <see cref="AttachInfoBadge"/> 在卡片右上角放一个小「i」按钮挂本组件——
/// 这样切换/选中点卡片不会误触描述,只有点小「i」才弹描述。
/// 用法:给元素设 <see cref="itemId"/>;UI 服务从全局 <see cref="GameLoop"/> 上下文取。
/// </summary>
[RequireComponent(typeof(Graphic))]
public class ItemIconDescButton : MonoBehaviour, IPointerClickHandler
{
    public int itemId;

    private void Awake()
    {
        var g = GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || itemId <= 0) return;
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        var ui = ctx != null ? ctx.Get<UIMgr>() : null;
        ui?.Show<ItemDescPanel>(new ItemDescParam { itemId = itemId });
    }

    /// <summary>在卡片右上角加一个小「i」徽标按钮,点它弹道具描述(不影响点卡片其余区域的选中/购买)。运行时构建,无需改卡片预制体。</summary>
    public static ItemIconDescButton AttachInfoBadge(RectTransform card, int itemId, TMP_FontAsset font = null, float size = 56f)
    {
        var go = new GameObject("InfoBadge", typeof(RectTransform), typeof(Image), typeof(ItemIconDescButton));
        var rt = (RectTransform)go.transform;
        rt.SetParent(card, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f); // 右上角
        rt.anchoredPosition = new Vector2(-6f, -6f);
        rt.sizeDelta = new Vector2(size, size);
        rt.SetAsLastSibling(); // 盖在卡片内容之上,确保点得到

        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.45f, 0.85f, 0.95f); // 蓝色徽标
        img.raycastTarget = true;

        var labelGo = new GameObject("i", typeof(RectTransform));
        var lrt = (RectTransform)labelGo.transform;
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "i";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = UITheme.Font(34);
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;

        var comp = go.GetComponent<ItemIconDescButton>();
        comp.itemId = itemId;
        return comp;
    }
}
