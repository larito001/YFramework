using System;
using TMPro;
using UnityEngine.UI;

/// <summary>通用确认弹窗的参数:标题/正文/按钮文案 + 确认/取消回调。</summary>
public class ConfirmParam
{
    public string title = "提示";
    public string message = "";
    public string confirmText = "确认";
    public string cancelText = "取消";
    public Action onConfirm;
    public Action onCancel;
}

/// <summary>
/// 通用确认弹窗(Top 层)。用 <see cref="ConfirmParam"/> 传内容与回调,任意界面可复用:
/// <c>Show&lt;ConfirmPanel, ConfirmParam&gt;(new ConfirmParam { message = "...", onConfirm = () => {...} });</c>
/// 预制体由 <c>Tools/UI/Build ConfirmPanel Prefab</c> 生成。
/// </summary>
public class ConfirmPanel : UIPageBase<ConfirmParam>
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI confirmLabel;
    public TextMeshProUGUI cancelLabel;
    public Button confirmBtn;
    public Button cancelBtn;

    private ConfirmParam current;

    public override void OnLoad()
    {
        confirmBtn.onClick.AddListener(OnConfirm);
        cancelBtn.onClick.AddListener(OnCancel);
    }

    protected override void OnBeforeShow(ConfirmParam param)
    {
        current = param ?? new ConfirmParam();
        if (titleText != null) titleText.text = current.title;
        if (messageText != null) messageText.text = current.message;
        if (confirmLabel != null) confirmLabel.text = string.IsNullOrEmpty(current.confirmText) ? "确认" : current.confirmText;
        if (cancelLabel != null) cancelLabel.text = string.IsNullOrEmpty(current.cancelText) ? "取消" : current.cancelText;
    }

    private void OnConfirm()
    {
        var cb = current?.onConfirm;
        CloseSelf();      // 先关弹窗,回调里若再开别的界面不会被本窗的关闭顶掉
        cb?.Invoke();
    }

    private void OnCancel()
    {
        var cb = current?.onCancel;
        CloseSelf();
        cb?.Invoke();
    }

    public override void OnShow() { }
    public override void OnHide() { }
    public override void OnResize() { }
}
