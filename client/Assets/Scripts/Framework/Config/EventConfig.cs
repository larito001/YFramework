using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YOTO
{    
    public enum YOTOEventType
    {

        Space,
       
       //刷新
       RefreshRoleList,
       RefreshProgress,
       RefreshPlayerProperty,
       GameTimerNotify,
       LootTimerNotify,
       VotEndNotify,
       
       //
       ForceFood,
       UnForceFood,
       RefreshMainRule,
    }


}
