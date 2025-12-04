using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanel : UIPageBase
{
    public Button closeBtn;

    public override void OnLoad()
    {
    }

    public override void OnShow()
    {
        closeBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnHide()
    {
        closeBtn.onClick.RemoveAllListeners();
    }

    public override void OnResize()
    {
    }
}