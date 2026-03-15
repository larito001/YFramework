using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight guide page shell.
/// Extend this page with tutorial state and content when guidance is implemented.
/// </summary>
public class GuidePanel : UIPageBase
{   
    public Button btnClose;
    public override void OnLoad()
    {
    }

    public override void OnShow()
    {
        btnClose.onClick.RemoveAllListeners();
        btnClose.onClick.AddListener(CloseSelf);
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}
