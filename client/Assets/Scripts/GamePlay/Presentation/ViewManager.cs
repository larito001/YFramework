using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// todo：管理所有View的基类
/// </summary>
public abstract class ViewManager:IGameService
{
  protected Dictionary<int,BaseView> Views = new ();
  public void Init(GameContext ctx)
  {
    
  }

  public void Shutdown()
  {
    
  }
  public GameObject LoadBaseView(string viewName)
  {
    //todo: Views.Add();
    //todo:同步加载GameObject
    return null;
  }

  public void RemoveBaseView(int ID)
  {
    if (Views.ContainsKey(ID))
    {
      GameObject.DestroyImmediate(Views[ID].gameObject);
    }

    Views.Remove(ID);
  }


}

public abstract class BaseView:MonoBehaviour
{
  public int ID = -1;

  public abstract void Bind(Actor actor,int ID);
}