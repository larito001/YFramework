using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerManager : LogicPluginBase
{
    public static TowerManager Instance;

    public TowerManager()
    {
        Instance = this;
    }

    List<TowerBaseCtrlEntity> towers = new List<TowerBaseCtrlEntity>();
    public bool isEditorMode = false;

    public bool CheckTowerIsInRange(out TowerEntity tower)
    {
        foreach (var towerBaseCtrlEntity in towers)
        {
            tower = towerBaseCtrlEntity.GetTower();
            if (tower != null && tower.HaveObj)
            {
                return true;
            }
        }

        tower = null;
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