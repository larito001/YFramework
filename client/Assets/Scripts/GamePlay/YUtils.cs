using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class YUtils 
{
  public static bool  GetIUsableFromCollider(Collider other,out IUsable outModelBase)
  {
    if (other.TryGetComponent<SceneModelBase>(out SceneModelBase modelBase))
    {
      if (modelBase.GetObjectBase() is IUsable)
      {
        outModelBase = modelBase.GetObjectBase() as IUsable;
        return true;
      }
      
    }
    outModelBase=null;

    return false;
  }
}
