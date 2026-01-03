using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TrackFixEntity : ObjectBase, IVictim
{
    public float Rate = 0f;
    RateHud hud;
    private bool isInit = false;
    private float fixRate = 0f;
    private bool canFix = false;

    public void SetEntity(float rate)
    {
        fixRate = rate;
        SetPrefabBundlePath("InteractiveObjects/TrackFixPos");
    }

    public override string GetModelLayer()
    {
        return "Agent";
    }

    public override void OnColiderEnter(Collider other)
    {
        base.OnColiderEnter(other);
    }

    public override void YOTOUpdate(float deltaTime)
    {
        if (!isInit) return;
        base.YOTOUpdate(deltaTime);
    }

    public override void OnObjectClick()
    {
        base.OnObjectClick();
        if (!canFix)
        {
            TowerUpParam param = new TowerUpParam();

            List<Vector2Int> useIdAndNumber = new List<Vector2Int>();
            useIdAndNumber.Add(new Vector2Int(40001, 5));
            useIdAndNumber.Add(new Vector2Int(40002, 5));
            useIdAndNumber.Add(new Vector2Int(40003, 5));
            param.useIdAndNumber = useIdAndNumber;
            param.confirmAction = ConfirmFix;

            YFramework.uIMgr.Show(UIEnum.TowerUpPanel, param);
        }
    }

    private void ConfirmFix()
    {
        canFix = true;
    }

    public override void OnColiderExit(Collider other)
    {
        base.OnColiderExit(other);
    }

    protected override void AfterInstanceGObj()
    {
        hud = objTrans.GetComponentInChildren<RateHud>();
        hud.Reset();
        hud.Show();
        isInit = true;
        properties = new Properties();
        properties.Camp = Camp.Enemy;
        properties.Level = 1;
        properties.State = RoleState.Alive;
        properties.HP = 30;
        properties.MaxHP = 30;
        properties.OnDead = () =>
        {
            TowerManager.Instance.trackFixDic[fixRate] = true;

            bool isWin = true;
            foreach (var valueTemp in TowerManager.Instance.trackFixDic.Values)
            {
                if (!valueTemp)
                {
                    isWin = false;
                }
            }

            if (isWin)
            {
                YFramework.uIMgr.Show(UIEnum.WinPanel);
            }
            
            RecoverObject();
        };
        hud.UpdateRate(properties.HP / properties.MaxHP);
    }

    protected override void BeforeRecover(bool isDelete)
    {
        isInit = false;
    }

    Properties properties;

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (canFix&&fireRole is PlayerEntity)
        {
            properties.HP -= 1;
            hud.UpdateRate(properties.HP / properties.MaxHP);
            if (properties.HP ==1)
            {
                EnemiesManager.instance.OnNightGenerate(ObjTrans.position, 10);
            }
            else if (properties.HP == 6)
            {
                EnemiesManager.instance.OnNightGenerate(ObjTrans.position, 10);
            }
            else if (properties.HP == 12)
            {
                EnemiesManager.instance.OnNightGenerate(ObjTrans.position, 10);
            }
            else if (properties.HP == 18)
            {
                EnemiesManager.instance.OnNightGenerate(ObjTrans.position, 10);
            }
            else if (properties.HP == 24)
            {
                EnemiesManager.instance.OnNightGenerate(ObjTrans.position, 10);
            }
        }
    }

    public Vector3 GetPosition()
    {
        return ObjTrans.position;
    }

    public Vector3 GetForward()
    {
        return ObjTrans.forward;
    }

    public void OnHurtSomeone()
    {
    }

    public void OnSlowDown(float rate)
    {
    }
}