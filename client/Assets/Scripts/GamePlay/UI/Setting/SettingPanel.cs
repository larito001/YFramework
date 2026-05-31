using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置界面:三个页签——声音 / 按键 / 画面。页签内容各自挂 <see cref="SettingTabBase"/> 子类,激活时懒构建并绑定服务。
/// 本类只负责页签切换与返回;预制体由 <c>Tools/UI/Build SettingPanel Prefab</c> 生成。
/// </summary>
public class SettingPanel : UIPageBase
{
    public Button backBtn;
    public Button tabSoundBtn;
    public Button tabKeyBtn;
    public Button tabGraphicsBtn;
    public GameObject soundTab;
    public GameObject keyTab;
    public GameObject graphicsTab;

    private static readonly Color TabOn = new Color(0.30f, 0.55f, 0.85f, 1f);
    private static readonly Color TabOff = new Color(0.25f, 0.27f, 0.33f, 1f);

    public override void OnLoad()
    {
        backBtn.onClick.AddListener(CloseSelf);
        tabSoundBtn.onClick.AddListener(() => ShowTab(0));
        tabKeyBtn.onClick.AddListener(() => ShowTab(1));
        tabGraphicsBtn.onClick.AddListener(() => ShowTab(2));
    }

    public override void OnShow()
    {
        ShowTab(0);
    }

    private void ShowTab(int index)
    {
        if (soundTab != null) soundTab.SetActive(index == 0);
        if (keyTab != null) keyTab.SetActive(index == 1);
        if (graphicsTab != null) graphicsTab.SetActive(index == 2);
        SetTabColor(tabSoundBtn, index == 0);
        SetTabColor(tabKeyBtn, index == 1);
        SetTabColor(tabGraphicsBtn, index == 2);
    }

    private static void SetTabColor(Button btn, bool on)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = on ? TabOn : TabOff;
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}
