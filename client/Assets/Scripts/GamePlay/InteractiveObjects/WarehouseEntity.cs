using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class WarehouseEntity : ObjectBase
{
    HudAlwaysFaceToTransform hud;
    public void SetEntity()
    {
        SetPrefabBundlePath("InteractiveObjects/WareHouse");
    }
    
    // public override string GetModelLayer()
    // {
    //     return "Agent";
    // }

//     public override void OnColiderEnter(Collider other)
//     {
//         base.OnColiderEnter(other);
//         // if (other.TryGetComponent(out ThirdPlayerMoveCtrl player))
//         // {
//         //     hud.gameObject.SetActive(true);
//         // }
//     }
//
//     public override void OnObjectClick()
//     {
//         base.OnObjectClick();
// GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.WarehousePanel);
//     }
//
//     public override void OnColiderExit(Collider other)
//     {
//         base.OnColiderExit(other); ;
//         // if (other.TryGetComponent(out ThirdPlayerMoveCtrl player))
//         // {
//         //     hud.gameObject.SetActive(false);
//         // }
//     }

    protected override void AfterInstanceGObj()
    {
        hud = objTrans.GetComponentInChildren<HudAlwaysFaceToTransform>();
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }
}
