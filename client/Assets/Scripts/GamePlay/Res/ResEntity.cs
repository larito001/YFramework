using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class ResEntity : ObjectBase, PoolItem<CircleItemMarker>, IUsable
{
    public static DataObjPool<ResEntity, CircleItemMarker> pool =
        new DataObjPool<ResEntity, CircleItemMarker>("ResEntity", 50);

    public int itemId = -1;
    RateHud rateHud;
    public float rate = 0;
    private IEnumerator GetIE;

    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {
        rateHud = ObjTrans.GetComponentInChildren<RateHud>();
        rateHud.Reset();
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(CircleItemMarker serverData)
    {
       var data = BagPlugin.Instance.GetItemData(serverData.itemId);
        
        Location = serverData.transform.position;
        itemId = serverData.itemId;
        SetInVision(true);
        SetPrefabBundlePath("Res/"+data.path);
        InstanceGObj();
    }

    IUser currentUser = null;

    public void OnUse(IUser user)
    {
        if (currentUser == null)
        {
            currentUser = user;
            user.OnUsing();
            rate = 0;
            if (GetIE != null)
            {
                YFramework.Instance.StopCoroutine(GetIE);
                GetIE = null;
            }

            GetIE = GetRes();
            YFramework.Instance.StartCoroutine(GetIE);
        }
        else if (currentUser == user)
        {
            if (GetIE != null)
            {
                YFramework.Instance.StopCoroutine(GetIE);
                GetIE = null;
            }

            rateHud.Hide();

            currentUser.OnStopUsing();
            currentUser = null;
        }
    }

    public void UnUse(IUser user)
    {
        if (currentUser == user)
        {
            if (GetIE != null)
            {
                YFramework.Instance.StopCoroutine(GetIE);
                GetIE = null;
            }

            rateHud.Hide();

            currentUser.OnStopUsing();
            currentUser = null;
        }
    }

    public void OnComplete()
    {
        BagPlugin.Instance.AddItem(itemId, 1);
        currentUser.OnStopUsing();
        currentUser = null;
        pool.RecoverItem(this);
        rateHud.Hide();
    }

    WaitForSeconds wait = new WaitForSeconds(0.01f);

    IEnumerator GetRes()
    {
        rateHud.Show();
        while (rate < 1)
        {
            yield return wait;
            rate += 0.01f;
            rateHud.UpdateRate(rate);
        }

        OnComplete();
    }
}