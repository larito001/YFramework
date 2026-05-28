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

    /// <summary>创建塔。ownerActorId = 放置者 Actor.ID（玩家放置传玩家 ID；关卡预设可传 -1=无主，便于"塔被摧毁通知放置者" / 击杀归属计分。</summary>
    public Tower CreateTower(Vector3 position, int teamId = 1, int ownerActorId = -1, float maxHealth = 500f)
    {
        var tower = new Tower { TeamId = teamId, OwnerActorId = ownerActorId };
        tower.Add(new HealthComponent { InitialMaxHealth = maxHealth });
        tower.Add(new HitstopOnDamageComponent());
        tower.Add(new AutoDespawnComponent { Delay = 3f });
        // Targeting 先 Add：每帧扫敌 + 旋转 + 写 Owner.TargetActorId，WeaponComponent 后续读用
        tower.Add(new TowerTargetingComponent
        {
            Range = 15f,
            RotateLerpRate = 8f,
        });
        tower.Add(new TowerWeaponComponent
        {
            Weapons = new List<Weapon> { BuildTurretRifle() },
            SocketName = "",  // 空 = 武器直接挂塔 root（占位 prefab 没专门 socket），用 HandLocalPosition/Euler 控制相对位置
            AimTime = 0.5f,   // 锁敌后 0.5s telegraph，避免瞬响应
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
            // 视觉挂载偏移（武器 transform 相对持有者 root）：塔顶往前 0.4m
            // 让武器看起来"装在炮塔顶部正前方"。
            HandLocalPosition = new Vector3(0f, 1.5f, 0.4f),
            HandLocalEuler = Vector3.zero,
            // 逻辑 FireOrigin 偏移（子弹起点相对持有者 Position+Rotation）：与 HandLocalPosition 对齐
            // 保证视觉武器位置 = 弹道起点；塔 prefab 高度若变化两者一起调
            MuzzleLocalOffset = new Vector3(0f, 1.5f, 0.4f),
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
            Damage = new DamageSpec
            {
                BaseDamage = 20f,
                HitstopTier = HitstopTier.Short,  // 塔连射小卡肉
            },
            RecoilShakeIntensity = 0f,   // 塔不抖屏（玩家不在塔身上）
            RecoilShakeDuration = 0f,
            Effect = new LinearProjectileEffect
            {
                BulletSpeed = 80f,
                BulletLifetime = 2f,
            },
        });
        // 不装 ReloadComponent——无限弹药不需要换弹
        return w;
    }
}
