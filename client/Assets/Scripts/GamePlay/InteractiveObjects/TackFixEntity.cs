using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TrackFixEntity : ObjectBase
{
    public float Rate = 0f;
    private bool isInFix = false;
    RateHud hud;
    private bool isInit = false;
    private float fixRate = 0f;
    public void SetEntity(float rate)
    {
        fixRate=rate;
        SetPrefabBundlePath("InteractiveObjects/TrackFixPos");
    }

    public override string GetModelLayer()
    {
        
        return "Agent";
    }
    public override void OnColiderEnter(Collider other)
    {
        base.OnColiderEnter(other);
        if (other.TryGetComponent(out ThirdPlayerMoveCtrl player))
        {
            isInFix = true;
        }
    }

    public override void YOTOUpdate(float deltaTime)
    {
        if (!isInit) return;
        base.YOTOUpdate(deltaTime);
        if (isInFix)
        {
            Rate+= deltaTime*10;
            if (Rate >= 100)
            {
                TowerManager.Instance.trackFixDic[fixRate] = true;
                RecoverObject();
                isInFix = false;
            }
            hud.UpdateRate(Rate/100);
        }
        else
        {
            Rate-= deltaTime*10;
            if (Rate < 0)
            {
                Rate = 0;
                return;
            }
            hud.UpdateRate(Rate/100);
        }

    }

    public override void OnObjectClick()
    {
        base.OnObjectClick();
        //todo: 开始修理
    }

    public override void OnColiderExit(Collider other)
    {
        base.OnColiderExit(other); ;
        if (other.TryGetComponent(out ThirdPlayerMoveCtrl player))
        {
            isInFix = false;
        }
    }
    protected override void AfterInstanceGObj()
    {
        hud = objTrans.GetComponentInChildren<RateHud>();
        hud.Reset();
        hud.Show();
        isInit = true;
    }

    protected override void BeforeRecover(bool isDelete)
    {
        isInit = false;
    }
}