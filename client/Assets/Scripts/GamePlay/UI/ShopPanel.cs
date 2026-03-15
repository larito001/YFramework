using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder shop page shell.
/// It currently owns only close behavior and is ready for gameplay data binding.
/// </summary>
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
