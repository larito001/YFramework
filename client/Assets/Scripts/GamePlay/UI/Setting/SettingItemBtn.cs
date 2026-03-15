using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class SettingItemBtn : YOTOScrollViewItem
{
    public TextMeshProUGUI text;
    private SettingPanel panel;
    private int index = -1;

    public void SetBtnData(SettingPanel settingPanel, int itemIndex)
    {
        panel = settingPanel;
        text.text = panel.settingList[itemIndex];
        index = itemIndex;
    }

    public override void OnRenderItem()
    {
        base.OnRenderItem();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    public override void OnHidItem()
    {
        base.OnHidItem();
        GetComponent<Button>().onClick.RemoveAllListeners();
    }

    private void OnClick()
    {
        panel?.Resolve<SoundMgr>().PlaySFX("Sound/SFX_UI_Click_Designed_Pop_Open_2", 0.5f);
        panel?.ShowSetting(index);
    }
}
