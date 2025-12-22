using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RateHud : HudAlwaysFaceToTransform
{
    public Scrollbar scrollbar;

    public void Reset()
    {
        scrollbar.size = 0;
        Hide();
    }

    public void Show()
    {
        ForceLookAt();
        gameObject.SetActive(true);
        
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    public void UpdateRate(float rate)
    {
        scrollbar.size = rate;
    }
}