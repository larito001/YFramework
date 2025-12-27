using UnityEngine.UI;
using YOTO;

public class GameMainPanel : UIPageBase
{
    public Scrollbar scrollBar;
    public YOTOScrollView scrollView;
    public Button bagBtn;
    public override void OnLoad()
    {
        scrollView.Initialize();
        scrollView.SetRenderer(ItemRender);
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
        bagBtn.onClick.AddListener(OnBagBtnClick);
        OnRefresh();
    }

    private void RefreshTrainHP()
    {
        var property = PlayerManager.Instance.train.GetProperties();
        scrollBar.size = property.HP / property.MaxHP;
    }

    private void OnRefresh()
    {
        scrollView.SetData(BagPlugin.Instance.GetListCount>8?8:BagPlugin.Instance.GetListCount);
    }

    private void OnBagBtnClick()
    {
        YFramework.uIMgr.Show(UIEnum.BagPanel);
    }

    public override void OnHide()
    {
        YFramework.eventMgr.RemoveEventListener(YOTOEventType.RefreshBagList,OnRefresh);
        YFramework.eventMgr.RemoveEventListener(YOTOEventType.RefreshTrainHP,RefreshTrainHP);
    }

    public override void OnResize()
    {
       
    }
}