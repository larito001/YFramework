using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class TowerBaseHud : HudAlwaysFaceToTransform
{
    Canvas canvas;
    TowerEntity tower;
    public Button up;
    public Button fix;
    public Button remove;
    private bool isInit = false;

    public void Init(TowerEntity towerEntity)
    {
        tower = towerEntity;

        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            canvas.worldCamera = YFramework.cameraMgr.getMainCamera();
        }

        gameObject.SetActive(false);
        up.onClick.AddListener(OnClickUp);
        fix.onClick.AddListener(OnClickFix);
        remove.onClick.AddListener(OnClickRemove);
    }

    private void OnClickRemove()
    {
        tower.RemoveOnBase();
    }

    private void OnClickFix()
    {
    }

    private void OnClickUp()
    {
        tower.OnLevelUp();
        OnHide();
    }

    public void OnShow()
    {
        ForceLookAt();
        gameObject.SetActive(true);
    }


    public void OnHide()
    {
        gameObject.SetActive(false);
    }
}