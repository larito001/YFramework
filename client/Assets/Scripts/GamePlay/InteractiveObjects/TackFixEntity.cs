using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TrackFixEntity : ObjectBase
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

    // public override string GetModelLayer()
    // {
    //     return "Agent";
    // }
    //
    // public override void OnColiderEnter(Collider other)
    // {
    //     base.OnColiderEnter(other);
    // }
    //
    // public override void OnObjectClick()
    // {
    //     base.OnObjectClick();
    //     if (!canFix)
    //     {
    //         TowerUpParam param = new TowerUpParam();
    //
    //         List<Vector2Int> useIdAndNumber = new List<Vector2Int>();
    //         useIdAndNumber.Add(new Vector2Int(40001, 5));
    //         useIdAndNumber.Add(new Vector2Int(40002, 5));
    //         useIdAndNumber.Add(new Vector2Int(40003, 5));
    //         param.useIdAndNumber = useIdAndNumber;
    //         param.confirmAction = ConfirmFix;
    //
    //         GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.TowerUpPanel, param);
    //     }
    // }

    private void ConfirmFix()
    {
        canFix = true;
    }

    // public override void OnColiderExit(Collider other)
    // {
    //     base.OnColiderExit(other);
    // }

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


    

    public Vector3 GetPosition()
    {
        return ObjTrans.position;
    }

    List<Vector3> atkSlot = new List<Vector3>();

    public List<Vector3> GetAtkSlot()
    {
        atkSlot.Clear();
        if (ObjTrans != null)
        {
            atkSlot.Add(ObjTrans.position);
        }

        return atkSlot;
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