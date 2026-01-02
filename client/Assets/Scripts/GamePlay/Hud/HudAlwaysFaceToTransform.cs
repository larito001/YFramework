using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class HudAlwaysFaceToTransform : MonoBehaviour
{
   public static Camera camera;
   
   public void ForceLookAt()
   {
      if (camera != null)
      {
         transform.rotation = camera.transform.rotation;
      }
   }
   private void FixedUpdate()
   {
      ForceLookAt();
   }
}
