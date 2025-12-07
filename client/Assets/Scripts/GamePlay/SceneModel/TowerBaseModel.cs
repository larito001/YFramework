using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TowerBaseModel : SceneModelBase
{
    
    public UnityAction OnClickEvent;
    protected override void OnClick()
    {
        base.OnClick();
        if (OnClickEvent != null)
        {
            OnClickEvent();
        }
    }
}
