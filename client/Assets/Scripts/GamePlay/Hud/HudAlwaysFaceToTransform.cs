using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HudAlwaysFaceToTransform : MonoBehaviour
{
   public  static Transform Target=null;

   private void FixedUpdate()
   {
      if(Target!=null)
      //todo:当前物体，持续朝向目标
      transform.LookAt(Target);
      transform.Rotate(0,180,0);
   }
}
