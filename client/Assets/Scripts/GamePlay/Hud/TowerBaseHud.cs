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
    public Button onClose;
    private bool isInit = false;

    public void Init(TowerEntity towerEntity)
    {

        tower = towerEntity;

        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            canvas.worldCamera =  GameLoop.Instance.Ctx.Get<CameraMgr>().getMainCamera();
        }

        gameObject.SetActive(false);
        up.onClick.RemoveAllListeners();
        fix.onClick.RemoveAllListeners();
        remove.onClick.RemoveAllListeners();
        onClose.onClick.RemoveAllListeners();
        up.onClick.AddListener(OnClickUp);
        fix.onClick.AddListener(OnClickFix);
        remove.onClick.AddListener(OnClickRemove);
        onClose.onClick.AddListener(OnHide);
    }

    private void OnClickRemove()
    {
        tower.RemoveOnBase();
    }

    private void OnClickFix()
    {  TowerUpParam param = new TowerUpParam();
        var data = TowerManager.Instance.GetTowerDataById(tower.towerBaseCtrl.TowerId);
        param.useIdAndNumber = data.FixRes.ToList(); 
        param.confirmAction =OnFixConfirm;
    GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.TowerUpPanel, param);
    }

    private void OnFixConfirm()
    {
        if (this != null)
        {
            if (isInit&& tower.ObjTrans!=null)
            {
                tower.OnFix();    
            }
            OnHide();
        }
        
    
    }

    private void OnClickUp()
    {
        if (_level <3)
        {
            TowerUpParam param = new TowerUpParam();

            var data = TowerManager.Instance.GetTowerDataById(tower.towerBaseCtrl.TowerId);
            param.useIdAndNumber = data.LevelUpRes.ToList(); 
            param.confirmAction =OnUpConfirm;
            GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.TowerUpPanel, param);
        }
    }

    private void OnUpConfirm()
    {
        if (isInit&& tower.ObjTrans!=null)
        {
            tower.OnLevelUp();    
        }

        OnHide();
    }

    private int _level = 1;
    public void OnShow(int level)
    {
        _level=level;
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