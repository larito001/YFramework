using System;
using TMPro;
using UnityEngine.UI;
using YOTO;

/// <summary>通用确认弹窗的参数:标题/正文/按钮文案 + 确认/取消回调。</summary>
public class ConfirmParam
{
    public string title = "提示";
    public string message = "";
    public string confirmText = "确认";
    public string cancelText = "取消";
    public Action onConfirm;
    public Action onCancel;

    /// <summary>是否在弹窗底部展示横幅广告(原生浮层)。默认 false——仅需要的弹窗(如结束打猎结算确认)显式置 true。</summary>
    public bool showBottomBanner = false;
    /// <summary>横幅广告位标识(用于区分场景与统计),仅在 <see cref="showBottomBanner"/> 为 true 时生效。</summary>
    public string bannerPlacement = "confirm";
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

    public override void OnShow()
    {
        // 仅当本次弹窗显式要求时,在底部展示横幅广告(原生浮层)。未接入广告 / 非 Android 时取不到或空操作,自动跳过。
        if (current != null && current.showBottomBanner && Context.TryGet<IAdService>(out var ad))
            ad.ShowBottomBanner(current.bannerPlacement);
    }

    public override void OnHide()
    {
        // 关闭弹窗即收掉横幅,释放原生资源(避免横幅残留到其它界面)。
        if (current != null && current.showBottomBanner && Context.TryGet<IAdService>(out var ad))
            ad.HideBanner();
    }
    public override void OnResize() { }
}
