using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPlayerBehavior
{
   public void OnInit();
   public BasicBehavior BasicBehavior { get; set; }
}
