using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TowerSystem
{
    public List<TowerData> towerDatas = new List<TowerData>();

    private readonly List<TowerBaseCtrlEntity> towersBase = new List<TowerBaseCtrlEntity>();
    private FlyTextMgr flyTextMgr;
    private UIMgr uiMgr;

    public TowerBaseCtrlEntity CurrentClickBase;

    public bool CheckTowerIsInRange(out TowerEntity tower, Vector3 pos, float range)
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

        return tower != null;
    }

    public void GenerateTowerBaseAtTransform(Transform parent, Vector3 offset)
    {
        TowerBaseCtrlEntity ctrl = TowerBaseCtrlEntity.pool.GetItem(null);
        ctrl.Parent = parent;
        ctrl.Location = offset;
        ctrl.Parent = parent;
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

    public TowerEntity GetTowerById(int id, TowerBaseCtrlEntity parent)
    {
        TowerEntity towerEntity = null;
        foreach (var towerData in towerDatas)
        {
            if (towerData.Id != id)
            {
                continue;
            }

            bool canBuild = true;
            foreach (var vector2Int in towerData.UseIdAndNumber)
            {
            }

            if (canBuild)
            {
                towerEntity = TowerEntity.pool.GetItem(parent);
            }
            else
            {
                flyTextMgr.AddTextAtScreenCenter("璧勬簮涓嶈冻");
            }
        }

        return towerEntity;
    }

    public void AddBaseCtrl(TowerBaseCtrlEntity ctrl)
    {
        towersBase.Add(ctrl);
    }

    public void RemoveBaseCtrl(TowerBaseCtrlEntity ctrl)
    {
        towersBase.Remove(ctrl);
    }

    public void ClickTower(TowerBaseCtrlEntity ctrl)
    {
        if (CurrentClickBase == null)
        {
            CurrentClickBase = ctrl;
            uiMgr.Show(UIEnum.SelectTowerPanel);
        }
    }

    public void ClickGennerateTower(int id)
    {
        CurrentClickBase.GenerateTowerById(id);
        CurrentClickBase = null;
        uiMgr.Hide(UIEnum.SelectTowerPanel);
    }

    public void Init(GameContext ctx)
    {
        flyTextMgr = ctx.Get<FlyTextMgr>();
        uiMgr = ctx.Get<UIMgr>();

        var so = Resources.Load<TowerDataSO>("Config/TowerData");
        foreach (var soTowerData in so.TowerDatas)
        {
            towerDatas.Add(new TowerData(soTowerData));
        }

        Resources.UnloadAsset(so);
    }

    public void Shutdown()
    {
    }
}
