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
        var eventMgr = GetService<EventMgr>();
        eventMgr.Add(YOTOEventType.RefreshBagList, OnRefresh);
        eventMgr.Add(YOTOEventType.RefreshTrainHP, RefreshTrainHP);
        eventMgr.Add(YOTOEventType.RefreshTime, OnRefreshTime);
        bagBtn.onClick.RemoveListener(OnBagBtnClick);
        bagBtn.onClick.AddListener(OnBagBtnClick);
        OnRefreshTime();
        OnRefresh();
    }

    public override void OnHide()
    {
        var eventMgr = GetService<EventMgr>();
        eventMgr.Remove(YOTOEventType.RefreshBagList, OnRefresh);
        eventMgr.Remove(YOTOEventType.RefreshTrainHP, RefreshTrainHP);
        eventMgr.Remove(YOTOEventType.RefreshTime, OnRefreshTime);
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
        Show<BagPanel>();
    }
}
