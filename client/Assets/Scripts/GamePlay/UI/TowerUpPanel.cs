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

public class TowerUpPanel : UIPageBase
{
    public YOTOScrollView scrollView;
        public   TowerUpParam _param;
    public Button closeBtn;
    public Button confirmBtn;
    public override void OnLoad()
    {

    }

    private void OnConfirm()
    {

        if (BagPlugin.Instance.CheckIsEnoughAndUse(_param.useIdAndNumber))
        {
            _param.confirmAction?.Invoke();
            CloseSelf();
        }
        else
        {
            FlyTextMgr.Instance.AddTextAtScreenCenter("资源不足");
        }

    }

    public override void BeforeShow(object param)
    {
        base.BeforeShow(param);
        _param = param as TowerUpParam;
        scrollView.Initialize();
        scrollView.SetRenderer(ItemRender);
        
    }

    private void ItemRender(YOTOScrollViewItem arg1, int arg2)
    {
        var item = arg1 as CommonItem;
        item.SetData(_param.useIdAndNumber[arg2]);
    }

    public override void OnShow()
    {        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(CloseSelf);
        confirmBtn.onClick.RemoveAllListeners();
        confirmBtn.onClick.AddListener(OnConfirm);
        scrollView.SetData(_param.useIdAndNumber.Count);
    }

    public override void OnHide()
    {
        _param = null;
    }

    public override void OnResize()
    {
    }
}