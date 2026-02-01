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
        // scrollView.Initialize();
        // scrollView.SetRenderer(ItemRender);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int index)
    {
        var item = arg1 as CommonItem; 
        item.SetData(BagPlugin.Instance.GetItemByIndex(index));
    }

    public override void OnShow()
    {
        YFramework.eventMgr.AddEventListener(YOTOEventType.RefreshBagList,OnRefresh);
        YFramework.eventMgr.AddEventListener(YOTOEventType.RefreshTrainHP,RefreshTrainHP);
        YFramework.eventMgr.AddEventListener(YOTOEventType.RefreshTime,OnRefreshTime);
        bagBtn.onClick.AddListener(OnBagBtnClick);
        OnRefresh();
    }

    private void OnRefreshTime()
    {
        if (time.text != GameDayNightManager.Instance.GetTime())
        {
            if (GameDayNightManager.Instance.IsDay())
            {
                dayIcon.SetActive(true);
                nightIcon.SetActive(false);
            }
            else
            {
                dayIcon.SetActive(false);
                nightIcon.SetActive(true);
            }
        
            time.text = GameDayNightManager.Instance.GetTime();
        }
    
    }

    private void RefreshTrainHP()
    {
        var property = TrainManager.Instance.GetTrainVictim().GetProperties();
        scrollBar.fillAmount = property.HP / property.MaxHP;
    }

    private void OnRefresh()
    {
        // scrollView.SetData(BagPlugin.Instance.GetListCount>8?8:BagPlugin.Instance.GetListCount);
    }

    private void OnBagBtnClick()
    {
        YFramework.uIMgr.Show(UIEnum.BagPanel);
    }

    public override void OnHide()
    {
        YFramework.eventMgr.RemoveEventListener(YOTOEventType.RefreshBagList,OnRefresh);
        YFramework.eventMgr.RemoveEventListener(YOTOEventType.RefreshTrainHP,RefreshTrainHP);
        YFramework.eventMgr.RemoveEventListener(YOTOEventType.RefreshTime,OnRefreshTime);
    }

    public override void OnResize()
    {
       
    }
}