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

    public void TrackInit()
    {
        trackFixDic.Add(0.85f,false);
        trackFixDic.Add(0.42f,false);
        TrackFixEntity trackFix = new TrackFixEntity();
        trackFix.SetEntity(0.42f);
        trackFix.SetInVision(true);
        var trackFixobj = GameObject.Find("trackFixPos");
        trackFix.Location = trackFixobj.transform.position;
        trackFix.InstanceGObj();
        
        TrackFixEntity trackFix2 = new TrackFixEntity();
        trackFix2.SetEntity(0.85f);
        trackFix2.SetInVision(true);
        var trackFixobj2 = GameObject.Find("trackFixPos2");
        trackFix2.Location = trackFixobj2.transform.position;
        trackFix2.InstanceGObj();
    }
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
    public Dictionary<float,bool> trackFixDic = new Dictionary<float, bool>();
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
                    FlyTextMgr.Instance.AddTextAtScreenCenter("资源不足");
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
            YFramework.uIMgr.Show(UIEnum.SelectTowerPanel); 
        }
   
    }

    public void ClickGennerateTower(int id )
    {
        CurrentClickBase.GenerateTowerById(id);
        CurrentClickBase = null;
        YFramework.uIMgr.Hide(UIEnum.SelectTowerPanel);
    }
}