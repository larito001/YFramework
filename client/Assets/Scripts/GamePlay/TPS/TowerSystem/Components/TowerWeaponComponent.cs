using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 防御塔的"持枪人组件"（塔专属）。按 ARCHITECTURE 持枪人组件协议实现：
///   1. 持 List&lt;Weapon&gt;，Attach 时 Adopt + Mount 当前武器
///   2. 每帧读 <see cref="Tower.TargetActorId"/>（由 <see cref="TowerTargetingComponent"/> 写）反查目标 Actor
///   3. 写 currentWeapon.FireOrigin / FireDirection / FireTarget / FireIntent（FireOrigin 算法走 currentWeapon.MuzzleLocalOffset）
///   4. Detach 时 Despawn 所有 Weapon
///
/// **AI 锁敌不在本组件**：扫敌 + 旋转 Owner.Rotation 由 <see cref="TowerTargetingComponent"/> 负责。本组件只读 TargetActorId。
/// 拆开让"AI" 和"持枪人协议" 解耦，未来加塔升级 / 优先级目标等改 Targeting 即可。
///
/// **AimTime telegraph**：新目标锁定后等 <see cref="AimTime"/> 秒才开火，给玩家反应时间。目标变化时重置 timer。
///
/// **Tick 顺序**：必须在 TowerTargetingComponent 之后 Add（Factory 已固定）+ 在 WeaponManager.Tick 之前（GameBootstrapper 已固定）。
/// </summary>
public class TowerWeaponComponent : ITowerComponent
{
    /// <summary>装在塔上的武器列表。Factory 配，Attach 时全部 Adopt 给 WeaponManager。当前只用 Weapons[0]。</summary>
    public List<Weapon> Weapons = new List<Weapon>();
    /// <summary>武器挂载 socket 名（塔 prefab 上的子物体名）。空 = 武器直接挂塔 root。</summary>
    public string SocketName = "Muzzle";
    /// <summary>瞄准延迟（秒）：新目标锁定后等这么久才开火。0=无延迟，立即开火。
    /// 默认 0.5s 给玩家反应时间。同一目标持续锁定不重置。</summary>
    public float AimTime = 0.5f;

    private WeaponManager weaponMgr;
    private ActorWorld world;
    private Weapon currentWeapon;
    // telegraph 状态：目标变化时重置 aimedTimer
    private int prevTargetActorId = -1;
    private float aimedTimer;

    public override void Attach(Tower owner)
    {
        Ctx?.TryGet(out weaponMgr);
        Ctx?.TryGet(out world);

        // 持枪人组件协议 #1：Adopt 所有武器；Mount 第 0 把
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Adopt(Weapons[i]);
        }
        if (Weapons.Count > 0 && Weapons[0] != null)
        {
            currentWeapon = Weapons[0];
            weaponMgr?.Mount(currentWeapon, owner, SocketName);
        }
    }

    public override void Detach()
    {
        // 持枪人组件协议 #4：Despawn 所有武器
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Despawn(Weapons[i]);
        }
        // 清自己写过的字段
        if (currentWeapon != null) currentWeapon.FireIntent = false;
        currentWeapon = null;
        prevTargetActorId = -1;
        aimedTimer = 0f;
        weaponMgr = null;
        world = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || Owner.IsDead) return;
        if (currentWeapon == null || world == null) return;

        // 1. 读 Targeting 写的 TargetActorId 反查目标
        int curTargetId = Owner.TargetActorId;
        Actor target = curTargetId >= 0 ? world.Get<Actor>(curTargetId) : null;
        // 目标可能在本帧已死 / 被销毁，反查失败也按"无目标" 处理
        if (target != null && target.IsDead) target = null;

        // 2. AimTime telegraph：目标变化重置 timer；同一目标持续锁定 timer 累加
        if (curTargetId != prevTargetActorId)
        {
            aimedTimer = 0f;
            prevTargetActorId = curTargetId;
        }
        if (target != null) aimedTimer += dt;
        else aimedTimer = 0f;  // 失去目标也清 timer，下次锁敌重新等 AimTime

        // 3. 写开火意图 + FireOrigin / FireDirection / FireTarget（持枪人组件协议 #3）
        // FireOrigin 由武器侧配（currentWeapon.MuzzleLocalOffset）；
        // targetCenter 投影到 muzzle 同高水平面，保证子弹水平直击。
        if (target != null)
        {
            var muzzle = Owner.Position + Owner.Rotation * currentWeapon.MuzzleLocalOffset;
            var targetCenter = new Vector3(target.Position.x, muzzle.y, target.Position.z);
            var dir = targetCenter - muzzle;
            if (dir.sqrMagnitude < 1e-4f) dir = Owner.Rotation * Vector3.forward;
            dir.Normalize();

            currentWeapon.FireOrigin = muzzle;
            currentWeapon.FireDirection = dir;
            currentWeapon.FireTarget = targetCenter;
            // AimTime 过完才开火（telegraph）。FireComponent 自带 cooldown 节流，本组件只控"该不该开"
            currentWeapon.FireIntent = aimedTimer >= AimTime;
        }
        else
        {
            currentWeapon.FireIntent = false;
        }
    }
}
