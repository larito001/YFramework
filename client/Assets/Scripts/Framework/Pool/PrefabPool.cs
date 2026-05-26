using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 被 PrefabPool 池化的对象可选实现：取出/归还时各回调一次。
/// 子弹/casing/decal 这种带状态的对象在 OnDespawn 里清理 Rigidbody/Trail/Reference，避免被下次取出时看到上一次的尾巴。
/// </summary>
public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

/// <summary>
/// 同步、零 GC、prefab 引用驱动的组件池——TPS hot path（子弹/casing/弹孔/枪火）用这个。
/// 不做异步加载（prefab 直接由调用方持有）、不做超时回收（业务自己决定何时 Release）。
/// 不是 IGameService，是局部实例——通常挂在 Weapon、ParticleSpawner 等持有者上。
/// </summary>
public class PrefabPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform poolRoot;
    private readonly Stack<T> stack;
    private readonly int maxSize;
    private readonly bool ownsRoot;

    public int CountInactive => stack.Count;

    public PrefabPool(T prefab, Transform poolRoot = null, int prewarm = 0, int maxSize = 1024)
    {
        if (prefab == null)
        {
            Debug.LogError("[PrefabPool] prefab is null");
        }

        this.prefab = prefab;
        this.maxSize = maxSize;
        this.stack = new Stack<T>(prewarm > 0 ? prewarm : 16);

        if (poolRoot != null)
        {
            this.poolRoot = poolRoot;
            this.ownsRoot = false;
        }
        else
        {
            this.poolRoot = new GameObject($"[Pool] {(prefab != null ? prefab.name : typeof(T).Name)}").transform;
            this.ownsRoot = true;
        }

        for (int i = 0; i < prewarm; i++)
        {
            stack.Push(CreateInstance());
        }
    }

    public T Get() => Get(Vector3.zero, Quaternion.identity, null);
    public T Get(Vector3 position) => Get(position, Quaternion.identity, null);
    public T Get(Vector3 position, Quaternion rotation) => Get(position, rotation, null);

    public T Get(Vector3 position, Quaternion rotation, Transform parent)
    {
        T item = null;
        while (stack.Count > 0 && item == null)
        {
            item = stack.Pop();
        }

        if (item == null)
        {
            item = CreateInstance();
        }

        var tr = item.transform;
        if (parent != tr.parent)
        {
            tr.SetParent(parent, false);
        }
        tr.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);

        if (item is IPoolable p)
        {
            p.OnSpawn();
        }

        return item;
    }

    public void Release(T item)
    {
        if (item == null) return;

        if (item is IPoolable p)
        {
            p.OnDespawn();
        }

        item.gameObject.SetActive(false);

        if (stack.Count >= maxSize)
        {
            Object.Destroy(item.gameObject);
            return;
        }

        item.transform.SetParent(poolRoot, false);
        stack.Push(item);
    }

    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (stack.Count >= maxSize) break;
            stack.Push(CreateInstance());
        }
    }

    public void Clear()
    {
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            if (item != null) Object.Destroy(item.gameObject);
        }
    }

    public void Dispose()
    {
        Clear();
        if (ownsRoot && poolRoot != null)
        {
            Object.Destroy(poolRoot.gameObject);
        }
    }

    private T CreateInstance()
    {
        var inst = Object.Instantiate(prefab, poolRoot);
        inst.gameObject.SetActive(false);
        return inst;
    }
}
