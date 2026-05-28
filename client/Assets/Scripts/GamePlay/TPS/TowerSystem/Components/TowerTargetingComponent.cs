using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 防御塔的 AI 锁敌组件：每帧扫 ActorWorld 找最近敌人 + 旋转 Owner.Rotation 朝目标 + 写 Owner.TargetActorId。
/// 不写 Weapon 字段——开火逻辑由 <see cref="TowerWeaponComponent"/> 读 TargetActorId 反查目标后处理。
///
/// 拆出本组件让"塔 AI" 和"持枪人协议" 解耦：未来扩展（防空目标优先级 / 弱点检测 / 嘲讽机制 / 升级范围可视化）
/// 都改本组件，不动 TowerWeaponComponent。
///
/// **Tick 顺序**：必须在 TowerWeaponComponent 之前 Add（Factory 已固定）——本组件写 Owner.Rotation + TargetActorId，
/// WeaponComponent 后续读用。
///
/// 筛选条件：非自己 + 非中立（TeamId!=0）+ 阵营不同 + 有 HealthComponent + 未死 + 距离≤Range。
/// </summary>
public class TowerTargetingComponent : ITowerComponent
{
    /// <summary>锁敌最大距离（米）。超过 Range 不锁、Owner.TargetActorId 设 -1。</summary>
    public float Range = 15f;
    /// <summary>朝目标旋转的指数 lerp 速率（rad/s 量级）。值越大转头越快。</summary>
    public float RotateLerpRate = 8f;

    private ActorWorld world;
    // 扫敌 buffer：复用避免每帧 alloc。所有塔共享（单线程顺序 Tick，不会冲突）。
    private static readonly List<Actor> scanBuf = new List<Actor>(64);

    public override void Attach(Tower owner)
    {
        Ctx?.TryGet(out world);
    }

    public override void Detach()
    {
        if (Owner != null) Owner.TargetActorId = -1;  // 清自己写的字段
        world = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || Owner.IsDead) return;
        if (world == null) return;

        // 1. 扫敌
        Actor target = FindNearestEnemy();
        Owner.TargetActorId = target?.ID ?? -1;

        // 2. 朝目标水平旋转（y 维度不动）
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
    }

    /// <summary>遍历 ActorWorld 找最近的敌方 Actor。返回 null 表示无目标。
    /// 筛选条件：非自己 + 非中立 + 阵营不同 + 有 HealthComponent + 未死 + 距离≤Range（平方比较省 sqrt）。</summary>
    private Actor FindNearestEnemy()
    {
        scanBuf.Clear();
        world.AppendAll(scanBuf);

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
