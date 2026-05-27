using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建 Character：new Character + 装组件 + ViewManager 加载 prefab。
/// 同时按"配方"造 Weapon Actor（数据 + FireComponent 组合），交给 WeaponComponent 持有。
/// 组件 Tick 顺序按 Add 顺序：Aim 在 Move 之前（Move 反算 local 动画依赖 Aim 写入的 Rotation）。
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
