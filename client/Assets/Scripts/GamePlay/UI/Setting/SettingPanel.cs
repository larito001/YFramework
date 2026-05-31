using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置界面:竖屏单页,只保留声音设置(按键 / 画面页签已移除)。
/// 声音控件由 <see cref="SoundSettingsTab"/> 在激活时懒构建并绑定 SoundMgr;本类只负责显示与返回。
/// 预制体由 <c>Tools/UI/Build SettingPanel Prefab</c> 生成。
/// </summary>
public class SettingPanel : UIPageBase
{
    public Button backBtn;
    public GameObject soundTab;

    public override void OnLoad()
    {
        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        if (soundTab != null) soundTab.SetActive(true);
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}
