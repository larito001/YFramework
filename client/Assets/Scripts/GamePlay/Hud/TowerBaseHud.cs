using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        TowerUpParam param = new TowerUpParam();

        var data = TowerManager.Instance.GetTowerDataById(tower.towerBaseCtrl.TowerId);
        param.useIdAndNumber = data.LevelUpRes.ToList(); 
        param.confirmAction =OnUpConfirm;
        YFramework.uIMgr.Show(UIEnum.TowerUpPanel, param);


        OnHide();
    }

    private void OnUpConfirm()
    {
        if (isInit&& tower.ObjTrans!=null)
        {
            tower.OnLevelUp();    
        }

    }

    public void OnShow()
    {
        isInit = true;
        ForceLookAt();
        gameObject.SetActive(true);
    }


    public void OnHide()
    {
        isInit = false;
        gameObject.SetActive(false);
    }
}