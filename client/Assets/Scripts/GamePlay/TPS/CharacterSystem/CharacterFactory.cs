using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建 Character：new Character + 装组件 + ViewManager 加载 prefab。
/// 同时按"配方"造 Weapon Actor（数据 + FireComponent 组合），交给 WeaponComponent 持有。
///
/// 组件 Add 顺序（= Tick 顺序）固定为 Aim → Move → Weapon → Melee → Health，原因：
///   - Aim 在 Move 之前：Move 用 Aim 写入的 Rotation 反算 local 动画方向
///   - Weapon 在 Move 之后：Weapon 在事件回调里设 IsMeleeing，Move 下帧用到
///   - Melee 在 Move 之后：Melee 在前冲窗内覆写 WishVelocity，Move 在后会抹掉
///   - Health 顺序无所谓（不 Tick），放最后避免影响其他依赖
/// </summary>
public class CharacterFactory
{
    private ViewManager manager;

    public void BindViewManager(ViewManager manager)
    {
        this.manager = manager;
    }

    public Character CreateCharacter()
    {
        var character = new Character();
        character.Add(new AimComponent());
        character.Add(new MoveComponent());
        character.Add(new WeaponComponent
        {
            Weapons = new List<Weapon>
            {
                // 没有 Pistol 动画，两个槽都用同一份 Animator Controller，只换模型 + 播切枪过场
                BuildRifleA(),
                BuildRifleB(),
                // 第三槽：模型/控制器复用 Rifle，把 ProjectileFireComponent 换成 MissileFireComponent，
                // 弹道立刻变成贝塞尔曲线导弹，点哪飞哪。这就是组件替换的典型用法。
                BuildMissileLauncher(),
            },
        });
        // 近战自包含：V 键订阅 / swing 时长 / 命中窗 / 前冲 / hitbox 都在这里。
        // 默认 hitbox 是球，前方 0.8m 半径 1m，命中窗 0.25-0.55s 内做 OverlapSphere。
        character.Add(new MeleeComponent
        {
            MeleeType = 0,          // 0=Hard 枪托砸，1=Kick 前踢
            SwingDuration = 1.2f,
            HitStartTime = 0.25f,
            HitEndTime = 0.55f,
            HitRadius = 1.0f,
            HitForwardOffset = 0.8f,
            HitHeight = 1.0f,
            Damage = 30f,
            ForwardSpeed = 3f,
            ForwardDuration = 2f,   // 配合 SwingDuration=1.2，总位移 ~2.5m
            // HitLayers 默认全开。生产期建议改成只含敌人层。
        });
        character.Add(new HealthComponent
        {
            InitialMaxHealth = 100f,
        });
        manager.LoadBaseView<CharacterView>("Player/Player", character);
        return character;
    }

    /// <summary>步枪：全自动 600 RPM，单发 25 伤，子弹 80 m/s。</summary>
    private static Weapon BuildRifleA()
    {
        var w = new Weapon
        {
            Name = "Rifle A",
            ModelPath = "Weapon/RiflePlaceholder",
            LocalPosition = Vector3.zero,
            LocalEuler = Vector3.zero,
        };
        w.Add(new ProjectileFireComponent
        {
            FireInterval = 0.1f, Damage = 25f, BulletSpeed = 80f, BulletLifetime = 2f
        });
        return w;
    }

    /// <summary>手枪占位：半自动手感（0.3s 间隔），单发 40 伤，子弹 60 m/s。</summary>
    private static Weapon BuildRifleB()
    {
        var w = new Weapon
        {
            Name = "Rifle B",
            ModelPath = "Weapon/PistolPlaceholder",
            LocalPosition = Vector3.zero,
            LocalEuler = Vector3.zero,
        };
        w.Add(new ProjectileFireComponent
        {
            FireInterval = 0.3f, Damage = 40f, BulletSpeed = 60f, BulletLifetime = 2f
        });
        return w;
    }

    /// <summary>导弹发射器：模型 + Mount socket 完全复用 Rifle，行为换 MissileFireComponent。
    /// 弧线飞行 1s，点哪飞哪；命中沿途碰撞或到点引爆。</summary>
    private static Weapon BuildMissileLauncher()
    {
        var w = new Weapon
        {
            Name = "Missile Launcher",
            ModelPath = "Weapon/RiflePlaceholder",
            LocalPosition = Vector3.zero,
            LocalEuler = Vector3.zero,
        };
        w.Add(new MissileFireComponent
        {
            FireInterval = 0.6f,
            Damage = 80f,
            FlightDuration = 1.0f,
            ForwardPushDist = 2f,
            ArcHeight = 4f,
        });
        return w;
    }
}
