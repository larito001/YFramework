using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public  class SceneModelBase : MonoBehaviour
{
    protected ObjectBase _objBase;
    public int EntityID;
    
    public void Init(ObjectBase objBase)
    {
        _objBase = objBase;
        EntityID = _objBase._entityID;
        this.gameObject.layer = LayerMask.NameToLayer(_objBase.GetModelLayer());
        objBase.AfterModelColiderInit();
    }

    public ObjectBase GetObjectBase()
    {
        return _objBase;
    }

    public void Remove()
    {
        if (_objBase != null)
        _objBase.BeforeModelColiderRemove();
        _objBase = null;
        EntityID = -1;
    }


    #region 外部触发

    
    private void OnTriggerEnter(Collider other)
    {
        if (_objBase != null)
        _objBase.OnColiderEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (_objBase != null)
        _objBase.OnColiderStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if(_objBase!=null)
        _objBase.OnColiderExit(other);
    }



    public void OnMouseClick()
    {
        if (_objBase != null)
        _objBase.OnObjectClick();
    }

    #endregion
}