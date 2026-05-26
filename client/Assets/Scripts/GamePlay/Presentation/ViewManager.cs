using System.Collections.Generic;
using UnityEngine;
using YOTO;

/// <summary>
/// View 管理基类：同步加载 prefab、实例化、Bind Actor、注册到字典。
/// 资源走 ResMgr.Load（同步，内部用 Resources.Load + 引用计数）。
/// prefab 必须放在 Resources/ 下，路径不带扩展名。
/// </summary>
public abstract class ViewManager : IGameService
{
    protected readonly Dictionary<int, BaseView> Views = new Dictionary<int, BaseView>();

    /// 记录每个 view 用的 prefab 路径，移除时 Release 引用计数。
    private readonly Dictionary<int, string> viewPaths = new Dictionary<int, string>();

    private ResMgr resMgr;

    public virtual void Init(GameContext ctx)
    {
        resMgr = ctx.Get<ResMgr>();
    }

    public virtual void Shutdown()
    {
        foreach (var view in Views.Values)
        {
            if (view != null)
            {
                Object.Destroy(view.gameObject);
            }
        }

        if (resMgr != null)
        {
            foreach (var path in viewPaths.Values)
            {
                resMgr.Release<GameObject>(path);
            }
        }

        Views.Clear();
        viewPaths.Clear();
        resMgr = null;
    }

    /// <summary>
    /// 加载 prefab、实例化、Bind 到 owner、注册到 Views 字典。一次性完成。
    /// 返回类型化的 view 组件；失败返回 null（已记错）。
    /// </summary>
    public TView LoadBaseView<TView>(string viewName, Actor owner) where TView : BaseView
    {
        if (resMgr == null)
        {
            Debug.LogError("[ViewManager] ResMgr 未注入，Init 没跑过？");
            return null;
        }

        if (owner == null)
        {
            Debug.LogError("[ViewManager] owner 为 null");
            return null;
        }

        var prefab = resMgr.Load<GameObject>(viewName);
        if (prefab == null)
        {
            Debug.LogError($"[ViewManager] 加载 prefab 失败: {viewName}");
            return null;
        }

        var go = Object.Instantiate(prefab);
        var view = go.GetComponent<TView>();
        if (view == null)
        {
            Debug.LogError($"[ViewManager] prefab '{viewName}' 上找不到 {typeof(TView).Name}");
            Object.Destroy(go);
            resMgr.Release<GameObject>(viewName);
            return null;
        }

        view.Bind(owner, owner.ID);

        if (Views.ContainsKey(view.ID))
        {
            Debug.LogError($"[ViewManager] view ID 冲突: {view.ID}（前一个 view 将被覆盖）");
            RemoveBaseView(view.ID);
        }

        Views[view.ID] = view;
        viewPaths[view.ID] = viewName;
        return view;
    }

    public void RemoveBaseView(int id)
    {
        if (Views.TryGetValue(id, out var view))
        {
            if (view != null)
            {
                Object.Destroy(view.gameObject);
            }
            Views.Remove(id);
        }

        if (viewPaths.TryGetValue(id, out var path))
        {
            resMgr?.Release<GameObject>(path);
            viewPaths.Remove(id);
        }
    }
}

public abstract class BaseView : MonoBehaviour
{
    public int ID = -1;

    public abstract void Bind(Actor actor, int ID);
}
