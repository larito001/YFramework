using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建 Tower：new Tower + 装组件 + ViewManager 加载 prefab + 配武器。
///
/// MVP 配方：HealthComponent + Hitstop + AutoDespawn + TowerWeaponComponent（持一把 Linear 步枪）。
/// 不装 GravityComponent——塔固定不动，IsGrounded 不读、WishVelocity 不写、CC 不需要。
///
/// 占位 view prefab 路径 "Tower/Tower"——按现有约定，Resources 下放一个简单的 cube + Muzzle 子物体即可。
/// 若 prefab 不存在，view 加载会失败但 Tower Actor + 组件仍然存在（开火走 FireOrigin 自己算）。
/// </summary>
public class TowerFactory
{
    private ViewManager manager;

    public void BindViewManager(ViewManager manager)
    {
        this.manager = manager;
    }

    public Tower CreateTower(Vector3 position, int teamId = 1, float maxHealth = 500f)
    {
        var tower = new Tower { TeamId = teamId };
        tower.Add(new HealthComponent { InitialMaxHealth = maxHealth });
        tower.Add(new HitstopOnDamageComponent());
        tower.Add(new AutoDespawnComponent { Delay = 3f });
        tower.Add(new TowerWeaponComponent
        {
            Weapons = new List<Weapon> { BuildTurretRifle() },
            SocketName = "",  // 空 = 武器直接挂塔 root（占位 prefab 没专门 socket），用 HandLocalPosition/Euler 控制相对位置
            Range = 15f,
            MuzzleHeight = 1.5f,
            RotateLerpRate = 8f,
        });

        var view = manager.LoadBaseView<TowerView>("Tower/Tower", tower);
        if (view != null)
        {
            view.gameObject.name = $"Tower_{tower.ID}";
            view.transform.position = position;
            tower.Position = position;
        }
        return tower;
    }

    /// <summary>塔用步枪：全自动 200 RPM、单发 20 伤、子弹 80 m/s、无限弹药（MagCapacity=0）。
    /// 比玩家 RifleA 节奏慢一倍，避免单塔伤害过爆；无限弹药省塔自动换弹的麻烦。</summary>
    private static Weapon BuildTurretRifle()
    {
        var w = new Weapon
        {
            Name = "Turret Rifle",
            ModelPath = "Weapon/RiflePlaceholder",
            // 武器相对塔 root 的偏移：塔顶往前 0.4m，y 与 TowerWeaponComponent.MuzzleHeight 对齐
            // 让武器看起来"装在炮塔顶部正前方"。塔 prefab 高度若变化，调这里 + MuzzleHeight 保持一致
            HandLocalPosition = new Vector3(0f, 1.5f, 0.4f),
            HandLocalEuler = Vector3.zero,
            // 塔不切枪，BackLocal* 留默认，永远用不上
            BackLocalPosition = Vector3.zero,
            BackLocalEuler = Vector3.zero,
            MagCapacity = 0,        // 0 = 无限弹药（FireComponent 不扣不查）
            CurrentAmmo = 0,
            HeavyRecoil = false,
            RecoilAnimSpeed = 1f,
        };
        w.Add(new FireComponent
        {
            FireInterval = 0.3f,
            Damage = 20f,
            RecoilShakeIntensity = 0f,   // 塔不抖屏（玩家不在塔身上）
            RecoilShakeDuration = 0f,
            Effect = new LinearProjectileEffect
            {
                BulletSpeed = 80f,
                BulletLifetime = 2f,
                HitstopTier = HitstopTier.Short,
            },
        });
        // 不装 ReloadComponent——无限弹药不需要换弹
        return w;
    }
}
