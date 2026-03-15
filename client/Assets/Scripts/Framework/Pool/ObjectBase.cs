using HotUpdate.Scripts.Framework.Pool.newPool;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Base class for pooled scene objects. It handles async loading, activation, and recovery.
/// </summary>
public abstract class ObjectBase
{
    private string prefabPath;
    private bool isVisible;
    private bool isInstantiated;
    private bool pendingRecover;
    private bool hasObject;

    protected Transform objTrans;

    private ObjectPool.PoolBuffer poolBuffer;
    private Vector3 location;
    private Quaternion rotation;
    private Transform parent;
    private SceneModelBase modelBase;

    public bool HaveObj => hasObject;
    public Transform ObjTrans => objTrans;

    public virtual Vector3 Location
    {
        get => location;
        set
        {
            location = value;
            if (objTrans != null)
            {
                objTrans.localPosition = value;
            }
        }
    }

    public virtual Quaternion Rotation
    {
        get => rotation;
        set
        {
            rotation = value;
            if (objTrans != null)
            {
                objTrans.localRotation = value;
            }
        }
    }

    public virtual Transform Parent
    {
        get => parent;
        set
        {
            parent = value;
            if (objTrans != null)
            {
                objTrans.SetParent(value);
            }
        }
    }

    protected void SetPrefabBundlePath(string path)
    {
        prefabPath = path;
    }

    public void SetInVision(bool inVision)
    {
        isVisible = inVision;
        if (objTrans != null)
        {
            objTrans.gameObject.SetActive(inVision);
        }
    }

    public void InstanceGObj()
    {
        pendingRecover = false;
        if (string.IsNullOrEmpty(prefabPath) || isInstantiated)
        {
            return;
        }

        EnsurePoolBuffer();
        poolBuffer.AsyncLoadAndGetItem(OnPrefabLoadFinished);
        isInstantiated = true;
    }

    public void RecoverObject(bool isDelete = false)
    {
        BeforeRecover(isDelete);
        RecoverLoadedObject();
        isVisible = false;
        isInstantiated = false;
        pendingRecover = true;
    }

    protected virtual float GetPoolBufferLoopCheckCdTime()
    {
        return 0.5f;
    }

    protected virtual uint GetPoolBufferInactiveTimeMax()
    {
        return 5;
    }

    protected virtual uint GetPoolBufferPerFrameDisposeCountMax()
    {
        return 10;
    }

    protected virtual uint GetPoolBufferPoolSizeMax()
    {
        return 30;
    }

    protected abstract void AfterInstanceGObj();
    protected abstract void BeforeRecover(bool isDelete);

    private void EnsurePoolBuffer()
    {
        if (poolBuffer != null)
        {
            return;
        }

        poolBuffer = GameLoop.Instance.Ctx.Get<ObjectPool>().GetBuffer(
            prefabPath,
            GetPoolBufferLoopCheckCdTime(),
            GetPoolBufferInactiveTimeMax(),
            GetPoolBufferPerFrameDisposeCountMax(),
            GetPoolBufferPoolSizeMax());
    }

    private void OnPrefabLoadFinished(GameObject origin, string name, bool isNew)
    {
        if (origin == null)
        {
            return;
        }

        hasObject = true;
        objTrans = origin.transform;

        if (pendingRecover)
        {
            RecoverLoadedObject();
            return;
        }

        BindSceneModel();
        objTrans.gameObject.SetActive(isVisible);
        ApplyTransform();
        AfterInstanceGObj();
    }

    private void BindSceneModel()
    {
        if (!objTrans.TryGetComponent(out modelBase))
        {
            modelBase = objTrans.gameObject.AddComponent<SceneModelBase>();
        }

        modelBase.Init(this);
    }

    private void ApplyTransform()
    {
        objTrans.SetParent(Parent);
        objTrans.localPosition = Location;
        objTrans.localRotation = Rotation;
    }

    private void RecoverLoadedObject()
    {
        hasObject = false;

        if (objTrans != null && modelBase != null)
        {
            modelBase.BeforeRemove();
            modelBase = null;
        }

        if (poolBuffer != null)
        {
            if (objTrans != null)
            {
                poolBuffer.RecoverItem(objTrans.gameObject);
            }

            poolBuffer.RemoveGetItemCompleteCallback(OnPrefabLoadFinished);
            poolBuffer = null;
            objTrans = null;
            return;
        }

        if (objTrans != null)
        {
            Object.Destroy(objTrans.gameObject);
            objTrans = null;
        }
    }
}
