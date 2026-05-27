using System.Collections.Generic;

/// <summary>
/// Weapon Actor 集合 + 每帧 Tick + 挂载状态写入。
/// 不直接产生 Weapon —— 由调用方（WeaponComponent / 后续的掉落系统）new 好 Weapon 对象，调 Adopt 移交。
/// Mount/Unmount 只改 Weapon 上的字段，view 端 WeaponView 自己监听响应。
/// </summary>
public class WeaponManager : IGameService, ITickable
{
    private ViewManager viewMgr;
    private ActorWorld world;
    private readonly List<Weapon> weapons = new List<Weapon>();

    public void Init(GameContext context)
    {
        viewMgr = context.Get<ViewManager>();
        world = context.Get<ActorWorld>();
    }

    public void Shutdown()
    {
        for (int i = weapons.Count - 1; i >= 0; i--)
        {
            var w = weapons[i];
            world.Unregister(w.ID);
            viewMgr.RemoveBaseView(w.ID);
            w.Dispose();
        }
        weapons.Clear();
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < weapons.Count; i++)
            weapons[i].Tick(dt);
    }

    /// <summary>接管一个外部构造好的 Weapon Actor：注册到 ActorWorld + 加载 view 模型。
    /// view prefab 是 mesh-only，WeaponView 组件运行时 AddComponent。</summary>
    public void Adopt(Weapon weapon)
    {
        if (weapon == null) return;
        weapons.Add(weapon);
        world.Register(weapon);
        viewMgr.LoadBaseView<WeaponView>(weapon.ModelPath, weapon, addIfMissing: true);
    }

    public void Despawn(Weapon weapon)
    {
        if (weapon == null) return;
        weapons.Remove(weapon);
        world.Unregister(weapon.ID);
        viewMgr.RemoveBaseView(weapon.ID);
        weapon.Dispose();
    }

    /// <summary>挂到手部 socket：写装备态 + 把 HandLocalPosition/Euler 拷到 LocalPosition/Euler 供 view 读。</summary>
    public void Mount(Weapon weapon, Character owner, string socketName)
    {
        if (weapon == null || owner == null) return;
        weapon.IsEquipped = true;
        weapon.OwnerCharacterId = owner.ID;
        weapon.MountSocketName = socketName;
        weapon.LocalPosition = weapon.HandLocalPosition;
        weapon.LocalEuler = weapon.HandLocalEuler;
    }

    /// <summary>挂到背部 socket（切枪过场用）：同 Mount 但拷的是 BackLocalPosition/Euler。</summary>
    public void MountOnBack(Weapon weapon, Character owner, string socketName)
    {
        if (weapon == null || owner == null) return;
        weapon.IsEquipped = true;
        weapon.OwnerCharacterId = owner.ID;
        weapon.MountSocketName = socketName;
        weapon.LocalPosition = weapon.BackLocalPosition;
        weapon.LocalEuler = weapon.BackLocalEuler;
    }

    /// <summary>卸下：清装备态。WeaponView 检测 IsEquipped=false 后 SetActive(false)。
    /// MountSocketName 保持上次值，下次 Mount 同 socket 时也能命中 changed-detection。</summary>
    public void Unmount(Weapon weapon)
    {
        if (weapon == null) return;
        weapon.IsEquipped = false;
        weapon.OwnerCharacterId = -1;
    }
}
