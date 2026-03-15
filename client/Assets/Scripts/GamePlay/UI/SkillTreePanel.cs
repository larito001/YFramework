using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder skill-tree page shell.
/// It remains registered so prefab wiring stays stable while the feature is implemented.
/// </summary>
public class SkillTreePanel : UIPageBase
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
