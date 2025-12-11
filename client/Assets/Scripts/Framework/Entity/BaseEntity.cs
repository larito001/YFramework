using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

public abstract  class BaseEntity 
{
    private static int  ids=0;
    public int  _entityID;
    private bool _isLoaded=false;
    public bool IsLoaded
    {
       get { return _isLoaded; } 
    }
    public  BaseEntity()
    {
        Init();
        _isLoaded = true;
    }

    public void Init()
    {
        if (_isLoaded) return;
        _entityID=ids++;
        YOTOOnload();
        _isLoaded = true;
        YFramework.entityMgr._AddEntity(this);
    }

    protected virtual void YOTOOnload()
    {
        
    }
    public virtual void YOTOUpdate(float deltaTime)
    {
        
    }

    public virtual void YOTONetUpdate()
    {
        
    }

    public virtual void YOTOFixedUpdate(float deltaTime)
    {
        
    }

    public virtual void YOTOOnHide()
    {
        
    }
    

    public void RemoveThis()
    {
        YOTOOnHide();
        _isLoaded = false;
        YFramework.entityMgr._RemoveEntity(this);
    }


}
