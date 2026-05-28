using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 防御塔的"持枪人组件 + AI 锁敌" 一体（MVP 阶段不拆）。继承 IActorComponent 是通用组件——
/// 只读写 Actor 基类字段（Position / Rotation / TeamId / IsDead），不依赖 Tower 专属字段。
///
/// 按 ARCHITECTURE "持枪人组件协议" 实现：
///   1. 持 List&lt;Weapon&gt;，Attach 时 Adopt + Mount 当前武器
///   2. 每帧扫 ActorWorld 找最近敌人（不同 TeamId 且非中立且有 HealthComponent 且未死且在 Range 内）
///   3. 朝目标水平旋转 Owner.Rotation（指数 lerp）
///   4. 写 currentWeapon.FireOrigin / FireDirection / FireTarget / FireIntent（有目标 = 开火）
///   5. Detach 时 Despawn 所有 Weapon
///
/// FireComponent 自己有 cooldown + 弹药门控，塔不重复判定——只负责"有没有目标"。
///
/// Tick 顺序：必须在 WeaponManager.Tick **之前**——本组件写 currentWeapon.FireIntent 等字段，
/// FireComponent 在 WeaponManager.Tick 里读取。
/// </summary>
public class TowerWeaponComponent : IActorComponent
{
    /// <summary>装在塔上的武器列表。Factory 配，Attach 时全部 Adopt 给 WeaponManager。当前只用 Weapons[0]。</summary>
    public List<Weapon> Weapons = new List<Weapon>();
    /// <summary>武器挂载 socket 名（塔 prefab 上的子物体名）。占位 prefab 没 socket 时直接挂塔 root。</summary>
    public string SocketName = "Muzzle";
    /// <summary>锁敌最大距离（米）。超过 Range 不锁、不开火。</summary>
    public float Range = 15f;
    /// <summary>枪口相对塔脚下 Position.y 的偏移（米），用于算 FireOrigin / FireTarget 的 Y。</summary>
    public float MuzzleHeight = 1.5f;
    /// <summary>朝目标旋转的指数 lerp 速率（rad/s 量级）。值越大转头越快。</summary>
    public float RotateLerpRate = 8f;

    private WeaponManager weaponMgr;
    private ActorWorld world;
    private Weapon currentWeapon;
    // 扫敌 buffer：复用避免每帧 alloc。GetAll 把 ActorWorld 当前所有 Actor 写进来。
    private static readonly List<Actor> scanBuf = new List<Actor>(64);

    public override void Attach(Actor owner)
    {
        base.Attach(owner);
        if (owner == null) return;
        Ctx?.TryGet(out weaponMgr);
        Ctx?.TryGet(out world);

        // 持枪人组件协议 #1：Adopt 所有武器；Mount 第 0 把（如有 socket）
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
        // 清写过的武器字段（cooldown 等由 FireComponent.Detach 管，本组件清自己写的"开火意图"链）
        if (currentWeapon != null)
        {
            currentWeapon.FireIntent = false;
        }
        currentWeapon = null;
        weaponMgr = null;
        world = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || Owner.IsDead) return;
        if (currentWeapon == null || world == null) return;

        // 1. 扫敌——找最近的"敌阵营 + 非中立 + 有 HealthComponent + 未死 + 在射程内" Actor
        Actor target = FindNearestEnemy();

        // 2. 旋转炮塔朝目标（仅水平，y 维度不动）
        if (target != null)
        {
            var toTarget = target.Position - Owner.Position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 1e-4f)
            {
                var targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                float t = 1f - Mathf.Exp(-RotateLerpRate * dt);
                Owner.Rotation = Quaternion.Slerp(Owner.Rotation, targetRot, t);
            }
        }

        // 3. 写开火意图 + FireOrigin / FireDirection / FireTarget（持枪人组件协议 #3）
        if (target != null)
        {
            var muzzle = Owner.Position + Vector3.up * MuzzleHeight + Owner.Rotation * Vector3.forward * 0.6f;
            var targetCenter = target.Position + Vector3.up * MuzzleHeight;  // 瞄目标的胸部高度
            var dir = (targetCenter - muzzle);
            if (dir.sqrMagnitude < 1e-4f) dir = Owner.Rotation * Vector3.forward;
            dir.Normalize();

            currentWeapon.FireOrigin = muzzle;
            currentWeapon.FireDirection = dir;
            currentWeapon.FireTarget = targetCenter;
            currentWeapon.FireIntent = true;  // 有目标即开火，FireComponent 自带 cooldown / 弹药门控
        }
        else
        {
            currentWeapon.FireIntent = false;
        }
    }

    /// <summary>遍历 ActorWorld 找最近的敌方 Actor。返回 null 表示无目标。
    /// 筛选条件：非自己 + 非中立 + 阵营不同 + 有 HealthComponent + 未死 + 距离≤Range（平方比较省 sqrt）。</summary>
    private Actor FindNearestEnemy()
    {
        scanBuf.Clear();
        world.GetAll(scanBuf);

        float bestSqr = Range * Range;
        Actor best = null;
        var selfPos = Owner.Position;
        int selfTeam = Owner.TeamId;

        for (int i = 0; i < scanBuf.Count; i++)
        {
            var a = scanBuf[i];
            if (a == null || a == Owner) continue;
            if (a.TeamId == 0 || a.TeamId == selfTeam) continue;  // 中立 / 同阵营跳过
            if (a.IsDead) continue;
            if (a.Get<HealthComponent>() == null) continue;       // 没 HP 组件的不打（Bullet / Weapon 等）

            var d = a.Position - selfPos;
            float sqr = d.x * d.x + d.y * d.y + d.z * d.z;
            if (sqr < bestSqr) { bestSqr = sqr; best = a; }
        }
        return best;
    }
}
