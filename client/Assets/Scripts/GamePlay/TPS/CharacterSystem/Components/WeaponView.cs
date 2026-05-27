using UnityEngine;

/// <summary>
/// 武器 view：被动消费 Weapon 的装备态字段，决定挂到角色 socket / 隐藏。
/// Weapon 完全不知道 view 存在；跨 actor 引用（找 owner 的 CharacterView）走 ViewManager.TryGetView。
/// 没有"落地武器"功能前，未装备就关掉所有 Renderer 隐形（不用 SetActive，否则 inactive 后 LateUpdate
/// 停跑、状态机收不到 Equip 信号，再也激活不回来）。
///
/// 运行时 AddComponent 到 mesh-only 武器 prefab 实例上，prefab 本身不带任何 MonoBehaviour。
/// </summary>
public class WeaponView : BaseView
{
    private Weapon weapon;
    private ViewManager viewMgr;
    private Renderer[] renderers;

    private int currentOwnerId = -2;
    private string currentSocketName;
    private bool currentEquipped;
    private bool initialized;

    public override void Bind(Actor actor, int id)
    {
        weapon = actor as Weapon;
        ID = id;
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx != null) ctx.TryGet(out viewMgr);
        renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    /// <summary>view 被 ViewManager.RemoveBaseView 销毁时清 Actor 引用，
    /// 与 BulletView.OnDespawn 对齐：避免 Unity Destroy 排队期间 LateUpdate 还跑一帧
    /// 命中已 Dispose 的 weapon 引用做出脏写。</summary>
    private void OnDestroy()
    {
        weapon = null;
        viewMgr = null;
        renderers = null;
        ID = -1;
    }

    private void LateUpdate()
    {
        if (weapon == null) return;

        bool changed = !initialized
            || weapon.IsEquipped != currentEquipped
            || weapon.OwnerCharacterId != currentOwnerId
            || weapon.MountSocketName != currentSocketName;
        if (!changed) return;

        ApplyMount();
        currentEquipped = weapon.IsEquipped;
        currentOwnerId = weapon.OwnerCharacterId;
        currentSocketName = weapon.MountSocketName;
        initialized = true;
    }

    private void ApplyMount()
    {
        // 未装备：关 Renderer 隐形。等以后做掉落/拾取再扩展（写世界坐标 + 显形 + 开 collider）
        if (!weapon.IsEquipped || weapon.OwnerCharacterId < 0)
        {
            SetRenderersEnabled(false);
            return;
        }

        if (viewMgr == null || !viewMgr.TryGetView(weapon.OwnerCharacterId, out var ownerView) || ownerView == null)
        {
            Debug.LogWarning($"[WeaponView] 找不到 owner view id={weapon.OwnerCharacterId}");
            return;
        }

        var socket = FindChildByName(ownerView.transform, weapon.MountSocketName);
        if (socket == null)
        {
            Debug.LogWarning($"[WeaponView] 找不到 socket '{weapon.MountSocketName}' on {ownerView.name}");
            return;
        }

        transform.SetParent(socket, worldPositionStays: false);
        transform.localPosition = weapon.LocalPosition;
        transform.localRotation = Quaternion.Euler(weapon.LocalEuler);
        SetRenderersEnabled(true);
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = enabled;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
