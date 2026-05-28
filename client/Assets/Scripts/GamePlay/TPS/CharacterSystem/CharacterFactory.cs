using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建 Character：new Character + 装组件 + ViewManager 加载 prefab。
/// 同时按"配方"造 Weapon Actor（数据 + FireComponent + ReloadComponent），交给 WeaponComponent 持有。
///
/// 组件 Add 顺序（= Tick 顺序）固定为 Aim → Move → Weapon → Melee → Gravity → Health → Hitstop → AutoDespawn，原因：
///   - Aim 在 Move 之前：Move 用 Aim 写入的 Rotation 反算 local 动画方向
///   - Melee 在 Move 之后：Melee 在前冲窗内覆写 WishVelocity.x/z，Move 在后会抹掉
///   - Gravity 在 Move/Melee 之后：x/z 由前面写完，Gravity 最后一锤定 y
///   - Health 顺序无所谓（不 Tick），但要在 Hitstop/AutoDespawn 之前 Add——它俩 Attach 时要 Get HealthComponent 订阅事件
///   - Hitstop/AutoDespawn 只订阅 OnDamaged/OnDied，顺序无所谓
///
/// Dummy 配方只装 HealthComponent + GravityComponent + 反馈/清理组件，没有 Aim/Move/Weapon/Melee。
/// </summary>
public class CharacterFactory
{
    private ViewManager manager;

    public void BindViewManager(ViewManager manager)
    {
        this.manager = manager;
    }

    public Character CreateCharacter(Vector3 position = default)
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
                // 第三槽：模型/控制器复用 Rifle，把 FireComponent.Effect 从 LinearProjectileEffect 换成 BezierMissileEffect，
                // 弹道立刻变成贝塞尔曲线导弹，点哪飞哪。这就是 effect 策略替换的典型用法。
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
        // 重力 + 贴地。写 WishVelocity.y，放在所有写 x/z 的组件之后
        character.Add(new GravityComponent());
        character.Add(new HealthComponent
        {
            InitialMaxHealth = 100f,
        });
        // 反馈 + 清理：订阅 HealthComponent 事件。把这些跨系统调用拆到独立组件而不是塞 HealthComponent 里，
        // 让 HealthComponent 只做 HP 数学 + 事件广播，分层清晰。
        character.Add(new HitstopOnDamageComponent());
        character.Add(new AutoDespawnComponent { Delay = 3f });

        var view = manager.LoadBaseView<CharacterView>("Player/Player", character);
        if (view != null && position != Vector3.zero)
        {
            TeleportTo(view, position);
            character.Position = position;
        }
        return character;
    }

    /// <summary>站桩敌人：只装 HealthComponent，没有 Aim/Move/Weapon/Melee。
    /// view 复用 Player.prefab（Dummy.fbx 模型 + CharacterController 胶囊 collider 当 hitbox）。
    /// 收到任意来源伤害（近战 / 子弹 / 射线 / 导弹）都会走 HealthComponent.ApplyDamage 扣血。
    /// 死亡后 IsDead=true，HealthComponent 内部拦截后续伤害；模型停在原地（没有死亡动画，留给后续接）。
    ///
    /// 后续要做"会动 / 会还击"的敌人：在 CreateDummy 基础上 Add AI 组件（如 SimpleAIComponent
    /// 写 WishVelocity / Rotation / IsShooting）+ MoveComponent + WeaponComponent，不需要造新 Actor 类。</summary>
    public Character CreateDummy(Vector3 position, float maxHealth = 1000f)
    {
        var character = new Character();
        // 给 Dummy 也装重力，spawn 后会被 Gravity + CC 一起拉到地面，避免悬空或半身埋在地形里
        character.Add(new GravityComponent());
        character.Add(new HealthComponent
        {
            InitialMaxHealth = maxHealth,
        });
        // Dummy 也接卡肉 + 自动清理（被打了也卡，死了 3s 后清）
        character.Add(new HitstopOnDamageComponent());
        character.Add(new AutoDespawnComponent { Delay = 3f });

        var view = manager.LoadBaseView<CharacterView>("Player/Player", character);
        if (view != null)
        {
            view.gameObject.name = $"Dummy_{character.ID}";
            TeleportTo(view, position);
            // 同步 actor 状态：Bind 时已用 transform.position 写过一次，但是 instantiate 后我们才设位置，
            // 这里再回写一遍保证 character.Position 与 view 一致。
            character.Position = position;
        }
        return character;
    }

    /// <summary>把带 CharacterController 的 view 安全传送到目标位置。
    /// 直接赋 transform.position 在 CC 启用时会被 CC 的内部物理 resolve 吞掉（表现为生成在原点），
    /// 必须先 disable CC、设位置、再 enable，这是 Unity 文档推荐的 CC 传送做法。</summary>
    private static void TeleportTo(CharacterView view, Vector3 position)
    {
        var cc = view.Controller;
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        view.transform.position = position;
        if (cc != null) cc.enabled = wasEnabled;
    }

    /// <summary>步枪：全自动 600 RPM，单发 25 伤，子弹 80 m/s，30 发弹匣。</summary>
    private static Weapon BuildRifleA()
    {
        var w = new Weapon
        {
            Name = "Rifle A",
            ModelPath = "Weapon/RiflePlaceholder",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            // 背挂位置/朝向占位：Spine1 是默认背骨，pose 全 0 大概率会穿模/方向不对，
            // 需要每把武器进 Unity 手调（先看到大致位置再细调）。
            BackLocalPosition = new Vector3(0f, 0.15f, -0.2f),
            BackLocalEuler = new Vector3(0f, 90f, 0f),
            MagCapacity = 30,
            CurrentAmmo = 30,
            HeavyRecoil = false,        // 小后坐力：ShootOnce
            RecoilAnimSpeed = 3.5f,     // 0.1s FireInterval，clip ~0.35s，加速到 ~0.1s 播完
        };
        w.Add(new FireComponent
        {
            FireInterval = 0.1f, Damage = 25f,
            RecoilShakeIntensity = 0.08f, RecoilShakeDuration = 0.06f,  // 全自动小抖
            // 全自动 → Short 卡肉，避免每发都把目标钉死、节奏被毁
            Effect = new LinearProjectileEffect { BulletSpeed = 80f, BulletLifetime = 2f, HitstopTier = HitstopTier.Short },
        });
        w.Add(new ReloadComponent { ReloadDuration = 1.5f });
        return w;
    }

    /// <summary>手枪占位：半自动手感（0.3s 间隔），单发 40 伤，子弹 60 m/s，12 发弹匣。</summary>
    private static Weapon BuildRifleB()
    {
        var w = new Weapon
        {
            Name = "Rifle B",
            ModelPath = "Weapon/PistolPlaceholder",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            // 背挂位置/朝向占位：Spine1 是默认背骨，pose 全 0 大概率会穿模/方向不对，
            // 需要每把武器进 Unity 手调（先看到大致位置再细调）。
            BackLocalPosition = new Vector3(0f, 0.15f, -0.2f),
            BackLocalEuler = new Vector3(0f, 90f, 0f),
            MagCapacity = 12,
            CurrentAmmo = 12,
            HeavyRecoil = false,        // 小后坐力：ShootOnce
            RecoilAnimSpeed = 1.2f,     // 0.3s FireInterval，clip ~0.35s，略加速
        };
        w.Add(new FireComponent
        {
            FireInterval = 0.3f, Damage = 40f,
            RecoilShakeIntensity = 0.18f, RecoilShakeDuration = 0.12f,  // 半自动单发大抖
            Effect = new LinearProjectileEffect { BulletSpeed = 60f, BulletLifetime = 2f },
        });
        w.Add(new ReloadComponent { ReloadDuration = 1.0f });
        return w;
    }

    /// <summary>导弹发射器：模型 + Mount socket 完全复用 Rifle，FireComponent.Effect 换成 BezierMissileEffect。
    /// 弧线飞行 1s，点哪飞哪；命中沿途碰撞或到点引爆。</summary>
    private static Weapon BuildMissileLauncher()
    {
        var w = new Weapon
        {
            Name = "Missile Launcher",
            ModelPath = "Weapon/RiflePlaceholder",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            // 背挂位置/朝向占位：Spine1 是默认背骨，pose 全 0 大概率会穿模/方向不对，
            // 需要每把武器进 Unity 手调（先看到大致位置再细调）。
            BackLocalPosition = new Vector3(0f, 0.15f, -0.2f),
            BackLocalEuler = new Vector3(0f, 90f, 0f),
            MagCapacity = 4,
            CurrentAmmo = 4,
            HeavyRecoil = true,         // 大后坐力：ShootGrenade
            RecoilAnimSpeed = 1.0f,     // 0.6s FireInterval > clip 长度，原速即可
        };
        w.Add(new FireComponent
        {
            FireInterval = 0.6f, Damage = 80f,
            RecoilShakeIntensity = 0.35f, RecoilShakeDuration = 0.2f,  // 重武器大震
            Effect = new BezierMissileEffect
            {
                FlightDuration = 1.0f,
                ForwardPushDist = 2f,
                ArcHeight = 4f,
            },
        });
        w.Add(new ReloadComponent { ReloadDuration = 2.5f });
        return w;
    }
}
