using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RateHud : HudAlwaysFaceToTransform
{
    public Image bar;

    public void Reset()
    {
        bar.fillAmount = 0;
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
        bar.fillAmount = rate;
    }
}