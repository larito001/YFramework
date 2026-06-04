using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 挂在道具图标(Image)上:左键点击图标弹出通用道具描述弹窗(<see cref="ItemDescPanel"/>)。
/// 商店/装备卡片用——点图标 = 看描述;卡片其余区域(名称/价格等)仍走卡片自身的选中/预览/出战/购买。
/// 因为本组件实现 <see cref="IPointerClickHandler"/> 且图标在卡片之上,图标区的点击会被它接走、不再冒泡到卡片按钮。
/// 用法:给图标 GameObject AddComponent 并设 <see cref="itemId"/> 即可;UI 服务从全局 <see cref="GameLoop"/> 上下文取。
/// </summary>
[RequireComponent(typeof(Graphic))]
public class ItemIconDescButton : MonoBehaviour, IPointerClickHandler
{
    public int itemId;

    private void Awake()
    {
        var g = GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true; // 图标默认不挡射线,这里打开才点得到
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || itemId <= 0) return;
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        var ui = ctx != null ? ctx.Get<UIMgr>() : null;
        ui?.Show<ItemDescPanel>(new ItemDescParam { itemId = itemId });
    }
}
