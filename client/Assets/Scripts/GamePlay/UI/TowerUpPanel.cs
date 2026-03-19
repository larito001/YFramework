using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TowerUpParam
{
    public List<Vector2Int> useIdAndNumber;
    public UnityAction confirmAction;
}

public class TowerUpPanel : UIPageBase<TowerUpParam>
{
    public YOTOScrollView scrollView;
    public Button closeBtn;
    public Button confirmBtn;

    public override void OnLoad()
    {
    }

    private void OnConfirm()
    {
        PageParam?.confirmAction?.Invoke();
        CloseSelf();
    }

    protected override void OnBeforeShow(TowerUpParam param)
    {
        scrollView.Initialize();
        scrollView.SetRenderer(ItemRender);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int arg2)
    {
        var item = arg1 as CommonItem;
        item.SetData(PageParam.useIdAndNumber[arg2]);
    }

    public override void OnShow()
    {
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(CloseSelf);
        confirmBtn.onClick.RemoveAllListeners();
        confirmBtn.onClick.AddListener(OnConfirm);
        scrollView.SetData(PageParam.useIdAndNumber.Count);
    }

    public override void OnHide()
    {
        closeBtn.onClick.RemoveAllListeners();
        confirmBtn.onClick.RemoveAllListeners();
    }

    public override void OnResize()
    {
    }
}
