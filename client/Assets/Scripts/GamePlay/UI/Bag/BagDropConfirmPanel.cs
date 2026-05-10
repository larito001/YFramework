using TMPro;
using UnityEngine.UI;
using YOTO;

public class BagDropConfirmPanel : UIPageBase<BagDropConfirmParam>
{
    public TextMeshProUGUI txt_message;
    public Button btn_confirm;
    public Button btn_cancel;

    public override void OnLoad()
    {
        if (btn_confirm != null) btn_confirm.onClick.AddListener(OnConfirmClick);
        if (btn_cancel != null) btn_cancel.onClick.AddListener(OnCancelClick);
    }

    protected override void OnBeforeShow(BagDropConfirmParam param)
    {
        if (txt_message != null && param != null)
        {
            txt_message.text = $"确认丢弃 {param.itemName} ×{param.count}？丢弃后无法找回。";
        }
    }

    public override void OnShow()
    {
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }

    private void OnConfirmClick()
    {
        var bag = GetService<BagManager>();
        if (bag != null && PageParam != null)
        {
            bag.DropSlot(PageParam.slotIndex);
        }
        CloseSelf();
    }

    private void OnCancelClick()
    {
        CloseSelf();
    }
}
