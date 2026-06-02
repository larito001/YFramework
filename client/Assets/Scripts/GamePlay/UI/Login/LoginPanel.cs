using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 登录界面(竖屏):全屏背景 + 标题 +(左上)版本号 +(右上)适龄提示 +(底部)「TapTap 登录」按钮 + 合规版号占位。
/// 启动时若静默自动登录失败,由 <see cref="GameBootstrapper"/> 展示本界面;点击登录成功后进入大厅 <see cref="StartPanel"/>。
/// 预制体由 <c>Tools/UI/Build LoginPanel Prefab</c> 程序化生成,脚本字段在那里接好。
///
/// 登录走渠道无关的 <see cref="ILoginService"/>(当前仅 TapTap);后续接其它登录模块时,
/// 在 builder 里按渠道多加按钮、这里多接一个点击委托即可,登录服务层无需改动。
/// </summary>
public class LoginPanel : UIPageBase
{
    [Header("按钮")]
    public Button btn_login;          // TapTap 登录
    public TextMeshProUGUI loginLabel; // 按钮文字(登录中切换为「登录中...」)

    [Header("文本")]
    public TextMeshProUGUI versionText; // 左上版本号
    public TextMeshProUGUI statusText;  // 失败/取消提示

    private ILoginService login;
    private bool busy; // 登录请求进行中:防连点

    public override void OnLoad()
    {
        Context.TryGet<ILoginService>(out login);
        if (btn_login != null) btn_login.onClick.AddListener(OnTapTapLoginClick);
    }

    public override void OnShow()
    {
        if (versionText != null) versionText.text = "v" + Application.version;
        if (statusText != null) statusText.text = string.Empty;
        SetBusy(false);
    }

    public override void OnHide() { }
    public override void OnResize() { }

    private void OnTapTapLoginClick()
    {
        if (busy) return;
        if (login == null)
        {
            Debug.LogError("[LoginPanel] ILoginService 未注册,无法登录。");
            return;
        }
        if (statusText != null) statusText.text = string.Empty;
        SetBusy(true);
        login.Login(LoginChannel.TapTap, OnLoginComplete);
    }

    private void OnLoginComplete(LoginResult res)
    {
        if (res.success)
        {
            Debug.Log($"[LoginPanel] 登录成功 user={res.account?.userId} name={res.account?.name}");
            Show<StartPanel>(); // 进入大厅(存档已在启动时读好)
            CloseSelf();        // 关闭登录界面
            return;
        }

        SetBusy(false);
        if (res.canceled)
        {
            if (statusText != null) statusText.text = "已取消登录";
        }
        else
        {
            Debug.LogError($"[LoginPanel] 登录失败:{res.error}");
            if (statusText != null) statusText.text = "登录失败,请重试";
        }
    }

    /// <summary>登录请求进行中:禁用按钮 + 文字切「登录中...」,避免连点与误以为无响应。</summary>
    private void SetBusy(bool value)
    {
        busy = value;
        if (btn_login != null) btn_login.interactable = !value;
        if (loginLabel != null) loginLabel.text = value ? "登录中..." : "TapTap 登录";
    }
}
