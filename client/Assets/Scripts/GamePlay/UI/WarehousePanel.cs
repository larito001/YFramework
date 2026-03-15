using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder warehouse page shell.
/// It currently exposes only close behavior and awaits inventory wiring.
/// </summary>
public class WarehousePanel : UIPageBase
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
