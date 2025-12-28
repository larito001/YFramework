using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class ResEntity : ObjectBase, PoolItem<CircleItemMarker>, IUsable
{
    public static DataObjPool<ResEntity, CircleItemMarker> pool =
        new DataObjPool<ResEntity, CircleItemMarker>("ResEntity", 50);

    public int itemId = -1;
    public int count = 1;
    ItemData itemData;
    RateHud rateHud;
    public float rate = 0;
    private IEnumerator GetIE;

    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {
        rateHud = ObjTrans.GetComponentInChildren<RateHud>(true);
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
        count=serverData.count;
        itemData= BagPlugin.Instance.GetItemData(serverData.itemId);
        
        Location = serverData.transform.position;
        itemId = serverData.itemId;
        SetInVision(true);
        var res = itemData.path;
        if (count ==3)
        {  
            res+= "_3";
        }else if (count == 5)
        {
            res+= "_5";
        }
        SetPrefabBundlePath("Res/"+res);
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

            if (rateHud!=null)
            {
                rateHud.Hide(); 
            }
      

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

            if (rateHud != null)
            {
                rateHud.Hide();
            }
    

            currentUser.OnStopUsing();
            currentUser = null;
        }
    }

    public void OnComplete()
    {
        FlyTextMgr.Instance.AddText(itemData.Name+"x"+count,objTrans.position);
        BagPlugin.Instance.AddItem(itemId, count);
        currentUser.OnStopUsing();
        currentUser = null;
        rateHud.Hide();
        SceneResManager.Instance.RemoveRes(this);

    }

    WaitForSeconds wait = new WaitForSeconds(0.01f);

    IEnumerator GetRes()
    {
        if (rateHud != null)
        {
            rateHud.Show(); 
        }
        else
        {
            yield break;
        }
        while (rate<0.2f)
        {
            yield return wait;
            rate += 0.01f;
            rateHud.UpdateRate(rate);
        }

        OnComplete();
    }
}