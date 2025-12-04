using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ModelType
{
    None,
    Shop,
    Tree,
    BigBag,
}
public abstract class SceneModelBase : MonoBehaviour
{

    public void Init(ModelType type )
    {
        this.modelType = type;
    }

    [SerializeField] protected ModelType modelType;

    protected virtual void OnEnter(Collider other)
    {
        
    }
    protected virtual void OnExit(Collider other)
    {
        
    }
    
    private void OnTriggerEnter(Collider other)
    {
        OnEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        OnExit(other);
    }
}
