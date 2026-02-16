using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TowerManager : LogicPluginBase
{
    public static TowerManager Instance;
    public List<TowerData> towerDatas = new List<TowerData>();

    public TowerManager()
    {
        Instance = this;
    }

    List<TowerBaseCtrlEntity> towersBase = new List<TowerBaseCtrlEntity>();
    
    public bool CheckTowerIsInRange(out TowerEntity tower, Vector3 pos,float range)
    {
        tower = null;
        foreach (var towerBaseCtrlEntity in towersBase)
        {
            var tempTower = towerBaseCtrlEntity.GetTower();
            if (tempTower != null && tempTower.HaveObj)
            {
                var tempdistance = (tempTower.ObjTrans.position - pos).magnitude;
                if (tempdistance < range)
                {
                    tower = tempTower;
                }
            }
        }

        if (tower != null) return true;
        return false;
    }

    public void GenerateTowerBaseAtTransform(Transform parent, Vector3 offset)
    {
        TowerBaseCtrlEntity ctrl = TowerBaseCtrlEntity.pool.GetItem(null);
        ctrl.Parent = parent;
        ctrl.Location = offset;
        ctrl.Parent = parent;
    }


    protected override void OnInstall()
    {
        base.OnInstall();
   
        var so = Resources.Load<TowerDataSO>("Config/TowerData");
        foreach (var soTowerData in so.TowerDatas)
        {
            towerDatas.Add(new TowerData(soTowerData));
        }

        Resources.UnloadAsset(so);
    }
    
    public TowerData GetTowerDataById(int id)
    {
        foreach (var towerData in towerDatas)
        {
            if (towerData.Id == id)
            {
                return towerData;
            }
        }
        return null;
    }
    
    protected override void OnUninstall()
    {
        base.OnUninstall();
    }


    public TowerEntity GetTowerById(int id,TowerBaseCtrlEntity  parent)
    {
        TowerEntity _towerEntity = null;
        foreach (var towerData in towerDatas)
        {
            if (towerData.Id == id)
            {
                bool canBuild = true;
                foreach (var vector2Int in towerData.UseIdAndNumber)
                {
                   var haveNum = BagPlugin.Instance.GetItemNum(vector2Int.x);
                   if (haveNum < vector2Int.y)
                   {
                       canBuild = false;
                   }
                }

                if (canBuild)
                {
                    foreach (var vector2Int in towerData.UseIdAndNumber)
                    {
                        BagPlugin.Instance.RemoveItem(vector2Int.x,vector2Int.y);
                    }

                    _towerEntity = TowerEntity.pool.GetItem(parent);
                }
                else
                {
                    GameLoop.Instance.Ctx.Get<FlyTextMgr>().AddTextAtScreenCenter("资源不足");
                }
                
            }
        }
       

     
        return _towerEntity;
    }
    
    public void AddBaseCtrl(TowerBaseCtrlEntity ctrl)
    {
        towersBase.Add(ctrl);
    }

    public void RemoveBaseCtrl(TowerBaseCtrlEntity ctrl)
    {
        towersBase.Remove(ctrl);
    }

    public TowerBaseCtrlEntity CurrentClickBase;
    public void ClickTower(TowerBaseCtrlEntity ctrl)
    {
        if (CurrentClickBase == null)
        {
            CurrentClickBase = ctrl;
            GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.SelectTowerPanel); 
        }
   
    }

    public void ClickGennerateTower(int id )
    {
        CurrentClickBase.GenerateTowerById(id);
        CurrentClickBase = null;
        GameLoop.Instance.Ctx.Get<UIMgr>().Hide(UIEnum.SelectTowerPanel);
    }
}