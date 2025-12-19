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

    List<TowerBaseCtrlEntity> towers = new List<TowerBaseCtrlEntity>();
    public bool isEditorMode = false;

    public void TrackInit()
    {
        trackFixDic.Add(0.95f,false);
        trackFixDic.Add(0.4f,false);
        TrackFixEntity trackFix = new TrackFixEntity();
        trackFix.SetEntity(0.4f);
        trackFix.SetInVision(true);
        var trackFixobj = GameObject.Find("trackFixPos");
        trackFix.Location = trackFixobj.transform.position;
        trackFix.InstanceGObj();
        
        TrackFixEntity trackFix2 = new TrackFixEntity();
        trackFix2.SetEntity(0.95f);
        trackFix2.SetInVision(true);
        var trackFixobj2 = GameObject.Find("trackFixPos2");
        trackFix2.Location = trackFixobj2.transform.position;
        trackFix2.InstanceGObj();
    }
    public bool CheckTowerIsInRange(out TowerEntity tower, Vector3 pos)
    {
        tower = null;
        float distance = 9999f;
        foreach (var towerBaseCtrlEntity in towers)
        {
            var tempTower = towerBaseCtrlEntity.GetTower();
            if (tempTower != null && tempTower.HaveObj)
            {
                var tempdistance = (tempTower.ObjTrans.position - pos).magnitude;
                if (tempdistance < distance)
                {
                    distance = tempdistance;
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
    
    protected override void OnUninstall()
    {
        base.OnUninstall();
    }

    public void SwitchMode()
    {
        isEditorMode = !isEditorMode;
        foreach (var towerBaseCtrlEntity in towers)
        {
            towerBaseCtrlEntity.OnSwitchMode(isEditorMode);
        }
    }

    public void AddBaseCtrl(TowerBaseCtrlEntity ctrl)
    {
        towers.Add(ctrl);
    }

    public void RemoveBaseCtrl(TowerBaseCtrlEntity ctrl)
    {
        towers.Remove(ctrl);
    }
}