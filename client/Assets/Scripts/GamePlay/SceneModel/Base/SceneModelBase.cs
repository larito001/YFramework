using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SceneModelBase : MonoBehaviour
{
    protected ObjectBase _objectBase;
    private I2DColliderHandler _2dCollider;
    private I2DTriggerHandler _2dTrigger;
    private I3DColliderHandler _3dCollider;
    private I3DTriggerHandler _3dTrigger;
    private IClickable _clickable;
    
    public bool TryGetEntity<T>(out T handler) where T : class
    {
        handler = _objectBase as T;
        return handler != null;
    }

    public void Init(ObjectBase objBase)
    {
        _objectBase = objBase;
        if (_objectBase is I2DColliderHandler)
        {
            _2dCollider = _objectBase as I2DColliderHandler;
        }

        if (_objectBase is I2DTriggerHandler)
        {
            _2dTrigger = _objectBase as I2DTriggerHandler;
        }

        if (_objectBase is I3DColliderHandler)
        {
            _3dCollider = _objectBase as I3DColliderHandler;
        }

        if (_objectBase is I3DTriggerHandler)
        {
            _3dTrigger = _objectBase as I3DTriggerHandler;
        }
        
        if (_objectBase is IClickable)
        {
            _clickable = _objectBase as IClickable;
        }
    }

    public void BeforeRemove()
    {
        _2dCollider = null;
        _2dTrigger = null;
        _3dCollider = null;
        _3dTrigger = null;
        _objectBase = null;
    }

    #region 2D碰撞

    private void OnCollisionEnter2D(Collision2D other)
    {
        _2dCollider?.On2DColliderEnter(other);
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        _2dCollider?.On2DColliderExit(other);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        _2dTrigger?.On2DTriggerEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _2dTrigger?.On2DTriggerExit(other);
    }

    #endregion

    #region 3D碰撞

    private void OnCollisionEnter(Collision other)
    {
        _3dCollider?.On3DColliderEnter(other);
    }

    private void OnCollisionExit(Collision other)
    {
        _3dCollider?.On3DColliderExit(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        _3dTrigger?.On3DTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        _3dTrigger?.On3DTriggerExit(other);
    }

    #endregion

    public void OnMouseClick()
    {
        _clickable?.OnClick();
    }
    
}