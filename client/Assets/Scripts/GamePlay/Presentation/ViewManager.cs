using System.Collections.Generic;
using UnityEngine;
using YOTO;

/// <summary>
/// View 管理服务：同步加载 prefab、实例化、Bind Actor、按 Actor.ID 注册到字典。
/// 全局唯一 service，所有 Actor 的 view 都进这一个字典。Actor.ID 全局唯一（Actor 基类静态计数器）保证不冲突。
/// 资源走 ResMgr.Load（同步，内部用 Resources.Load + 引用计数）。
/// prefab 必须放在 Resources/ 下，路径不带扩展名。
/// </summary>
public class ViewManager : IGameService
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
    ///
    /// addIfMissing=true：prefab 上没有 TView 组件时，运行时 AddComponent。
    /// 用于 mesh-only prefab（如武器模型）共用一份 view 行为类，避免每个 prefab 手挂同样组件。
    /// </summary>
    public TView LoadBaseView<TView>(string viewName, Actor owner, bool addIfMissing = false) where TView : BaseView
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
        if (view == null && addIfMissing)
            view = go.AddComponent<TView>();
        if (view == null)
        {
            Debug.LogError($"[ViewManager] prefab '{viewName}' 上找不到 {typeof(TView).Name}");
            Object.Destroy(go);
            resMgr.Release<GameObject>(viewName);
            return null;
        }

        view.Bind(owner, owner.ID);
        view.Owner = owner; // 注入 Owner，view 自行消费 Owner.Visible 做迷雾剔除

        if (Views.ContainsKey(view.ID))
        {
            Debug.LogError($"[ViewManager] view ID 冲突: {view.ID}（前一个 view 将被覆盖）");
            RemoveBaseView(view.ID);
        }

        Views[view.ID] = view;
        viewPaths[view.ID] = viewName;
        return view;
    }

    /// <summary>
    /// 注册一个**已在外部实例化好**的 view（模型在运行时拼装、不是单一 Resources prefab 的场景，如掉落物
    /// <see cref="DropItemSystem"/> 按品质拼模型 + 加物理）。走和 <see cref="LoadBaseView{TView}"/> 同一个
    /// Views 字典，统一 <see cref="TryGetView"/> 查询 + <see cref="RemoveBaseView"/> 清理；
    /// 因为没有 prefab 路径，<see cref="RemoveBaseView"/> 只 Destroy GameObject、不做 resMgr.Release。
    /// </summary>
    public TView RegisterView<TView>(TView view, Actor owner) where TView : BaseView
    {
        if (view == null)
        {
            Debug.LogError("[ViewManager] RegisterView: view 为 null");
            return null;
        }
        if (owner == null)
        {
            Debug.LogError("[ViewManager] RegisterView: owner 为 null");
            return null;
        }

        view.Bind(owner, owner.ID);
        view.Owner = owner; // 注入 Owner，view 自行消费 Owner.Visible 做迷雾剔除

        if (Views.ContainsKey(view.ID))
        {
            Debug.LogError($"[ViewManager] view ID 冲突: {view.ID}（前一个 view 将被覆盖）");
            RemoveBaseView(view.ID);
        }

        Views[view.ID] = view;
        // 不写 viewPaths：外部实例化的 view 无 prefab 引用计数，移除时只 Destroy 不 Release。
        return view;
    }

    /// <summary>按 Actor.ID 查 view。外部模块需要拿 view（如相机跟随、跨 actor reparent）走这里，不要持有 view 引用。</summary>
    public bool TryGetView(int id, out BaseView view) => Views.TryGetValue(id, out view);

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

    /// <summary>绑定的 Actor，由 <see cref="ViewManager"/> 在 Bind 后注入。各 view 读 <see cref="Actor.Visible"/>
    /// 自行做战争迷雾遮挡剔除（和 CharacterView 读 Owner.LocalScale 消费时间缩放同构），不再由 FogOfWarManager 反向操作 Renderer。</summary>
    public Actor Owner { get; set; }

    public abstract void Bind(Actor actor, int ID);

    // ── 战争迷雾遮挡剔除：view 自己消费 Owner.Visible ──
    private Renderer[] fogRenderers;   // 本 view 自身的 renderer（排除挂在子 view 下的，如挂角色身上的武器）
    private bool fogCached;
    private bool fogApplied;
    private bool fogLastVisible;

    /// <summary>按 <see cref="Actor.Visible"/> 开关本 view 自身的 Renderer。各 view 在 LateUpdate 末尾调一次。
    /// 仅在可见性翻转时操作 Renderer（缓存上次值），稳态零开销。</summary>
    protected void ApplyFogVisibility()
    {
        if (Owner == null) return;
        EnsureFogRenderers();
        bool vis = Owner.Visible;
        if (fogApplied && vis == fogLastVisible) return;
        fogApplied = true; fogLastVisible = vis;
        for (int i = 0; i < fogRenderers.Length; i++)
            if (fogRenderers[i] != null && fogRenderers[i].enabled != vis) fogRenderers[i].enabled = vis;
    }

    /// <summary>本 view 自身的 renderer 数组（懒收集 + 缓存）。WeaponView 等需要在可见性上再叠加自己的开关逻辑时取用。</summary>
    protected Renderer[] FogRenderers { get { EnsureFogRenderers(); return fogRenderers; } }

    /// <summary>renderer 结构变化（如增删挂件）后调用，下次 ApplyFogVisibility 重新收集。</summary>
    protected void InvalidateFogRenderers() { fogCached = false; }

    /// <summary>池化 view 回收时复位：重新启用 renderer + 清缓存标志，避免下次取出残留隐藏态。</summary>
    protected void ResetFogVisibility()
    {
        fogApplied = false; fogLastVisible = true; fogCached = false;
        // 不强制 enable 这里——下次 ApplyFogVisibility 会按新 Owner.Visible 应用；但池化对象先恢复可见更安全
    }

    private void EnsureFogRenderers()
    {
        if (fogCached && fogRenderers != null) return;
        fogRenderers = CollectOwnRenderers(this);
        fogCached = true;
    }

    /// <summary>取一个 view 自身的 renderer，**排除挂在子 view（如挂角色骨骼上的武器）下的 renderer**——
    /// 否则父角色一隐藏会连子 view 一起，或反之。判定：renderer 沿父链最近的 BaseView 必须是本 view。</summary>
    private static Renderer[] CollectOwnRenderers(BaseView view)
    {
        var all = view.GetComponentsInChildren<Renderer>(true);
        var own = new System.Collections.Generic.List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null) continue;
            if (r.GetComponentInParent<BaseView>() == view) own.Add(r);
        }
        return own.ToArray();
    }
}
