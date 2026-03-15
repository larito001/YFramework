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
        // item.SetData(BagPlugin.Instance.GetItemByIndex(index));
    }

    public override void OnShow()
    {
        GameLoop.Instance.Ctx.Get<EventMgr>().AddEventListener(YOTOEventType.RefreshBagList,OnRefresh);
        GameLoop.Instance.Ctx.Get<EventMgr>().AddEventListener(YOTOEventType.RefreshTrainHP,RefreshTrainHP);
        GameLoop.Instance.Ctx.Get<EventMgr>().AddEventListener(YOTOEventType.RefreshTime,OnRefreshTime);
        bagBtn.onClick.RemoveListener(OnBagBtnClick);
        bagBtn.onClick.AddListener(OnBagBtnClick);
        OnRefreshTime();
        OnRefresh();
    }

    private void OnRefreshTime()
    {
        var dayNightManager = GameLoop.Instance.Ctx.Get<GameDayNightManager>();
        var nextTimeText = dayNightManager.GetTime();
        if (time.text != nextTimeText)
        {
            if (dayNightManager.IsDay())
            {
                dayIcon.SetActive(true);
                nightIcon.SetActive(false);
            }
            else
            {
                dayIcon.SetActive(false);
                nightIcon.SetActive(true);
            }
        
            time.text = nextTimeText;
        }
    
    }

    private void RefreshTrainHP()
    {
        // var property = TrainManager.Instance.GetTrainVictim().GetProperties();
        // scrollBar.fillAmount = property.HP / property.MaxHP;
    }

    private void OnRefresh()
    {
        // scrollView.SetData(BagPlugin.Instance.GetListCount>8?8:BagPlugin.Instance.GetListCount);
    }

    private void OnBagBtnClick()
    {
        GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.BagPanel);
    }

    public override void OnHide()
    {
        GameLoop.Instance.Ctx.Get<EventMgr>().RemoveEventListener(YOTOEventType.RefreshBagList,OnRefresh);
        GameLoop.Instance.Ctx.Get<EventMgr>().RemoveEventListener(YOTOEventType.RefreshTrainHP,RefreshTrainHP);
        GameLoop.Instance.Ctx.Get<EventMgr>().RemoveEventListener(YOTOEventType.RefreshTime,OnRefreshTime);
        bagBtn.onClick.RemoveListener(OnBagBtnClick);
    }

    public override void OnResize()
    {
       
    }
}
