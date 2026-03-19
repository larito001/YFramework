using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class GameMainPanel : UIPageBase
{
    public Image scrollBar;
    public YOTOScrollView scrollView;
    public Button bagBtn;
    public TextMeshProUGUI time;
    public GameObject dayIcon;
    public GameObject nightIcon;

    public override void OnLoad()
    {
    }

    public override void OnShow()
    {
        GetService<EventMgr>().AddEventListener(YOTOEventType.RefreshBagList, OnRefresh);
        GetService<EventMgr>().AddEventListener(YOTOEventType.RefreshTrainHP, RefreshTrainHP);
        GetService<EventMgr>().AddEventListener(YOTOEventType.RefreshTime, OnRefreshTime);
        bagBtn.onClick.RemoveListener(OnBagBtnClick);
        bagBtn.onClick.AddListener(OnBagBtnClick);
        OnRefreshTime();
        OnRefresh();
    }

    public override void OnHide()
    {
        GetService<EventMgr>().RemoveEventListener(YOTOEventType.RefreshBagList, OnRefresh);
        GetService<EventMgr>().RemoveEventListener(YOTOEventType.RefreshTrainHP, RefreshTrainHP);
        GetService<EventMgr>().RemoveEventListener(YOTOEventType.RefreshTime, OnRefreshTime);
        bagBtn.onClick.RemoveListener(OnBagBtnClick);
    }

    public override void OnResize()
    {
    }

    private void OnRefreshTime()
    {

    }

    private void RefreshTrainHP()
    {
    }

    private void OnRefresh()
    {
    }

    private void OnBagBtnClick()
    {
        UIManager.Show(UIEnum.BagPanel);
    }
}
