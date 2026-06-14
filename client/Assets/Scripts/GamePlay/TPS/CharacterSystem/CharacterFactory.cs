using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建 Character：new Character + 装组件 + ViewManager 加载 prefab。
/// 同时按"配方"造 Weapon Actor（数据 + FireComponent + ReloadComponent），交给 WeaponComponent 持有。
///
/// 组件 Add 顺序（= Tick 顺序）固定为 Aim → Move → Weapon → SkillCast → Combo → Dodge → Gravity → Health → Hitstop → AutoDespawn，原因：
///   - Aim 在 Move 之前：Move 用 Aim 写入的 Rotation 反算 local 动画方向
///   - SkillCast / Dodge 在 Move 之后：释放时覆写 WishVelocity.x/z 做位移，Move 在后会抹掉
///   - Gravity 在 Move/SkillCast 之后：x/z 由前面写完，Gravity 最后一锤定 y
///   - Health 顺序无所谓（不 Tick），但要在 Hitstop/AutoDespawn 之前 Add——它俩 Attach 时要 Get HealthComponent 订阅事件
///   - Hitstop/AutoDespawn 只订阅 OnDamaged/OnDied，顺序无所谓
///
/// Dummy 配方只装 HealthComponent + GravityComponent + 反馈/清理组件，没有 Aim/Move/Weapon/SkillCast。
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
        var character = new Character { TeamId = 1, CurrentCharacterAnimSetPath = CharacterResPath.PlayerAnimSet };
        // 输入抽象：玩家 InputComponent 是 InputService + 相机的唯一消费者，对外只给世界空间意图。
        // 必须**最先 Add**——Aim/Move/Weapon/Skill 在 Attach 里 Owner.Get<InputComponentBase>() 拿它。
        character.Add(new InputComponent());
        character.Add(new AimComponent());
        // 瞄准常驻 → 瞄准移动(AimSpeed)成了默认行走速度，旧的 1.5 太慢，提到 4.5（接近原 WalkSpeed，仍明显慢于 Shift 冲刺 7）。
        // 脚步打滑就调 MoveComponent.AimAnimSpeed。冲刺/走路速度保持组件默认。
        character.Add(new MoveComponent { AimSpeed = 2.5f });
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
                // 第四槽：突击步枪——示范不同 WeaponAnimSet。Shoot clip 用 Rifle_ShootBurst（连发风格）跟 Pistol 视觉明显不同
                BuildBurstRifle(),
                // 第五槽（数字键 5）：近战 knife——无 FireComponent（不开火），左键放技能 1、V 放技能 2（见 BuildKnife + SkillPaths）
                BuildKnife(),
            },
        });
        // 技能列表（下标即 OnCastSkill/Cast 索引，必须与下方武器的 PrimarySkillIndex/SecondarySkillIndex 对齐）：
        //   [0] PlayerMelee  —— 常规枪 V 键近战（WeaponSecondarySkill=-1 回退到这）
        //   [1] PlayerKnife  —— knife 左键技能（BuildKnife.PrimarySkillIndex=1）；2 段连击
        //   [2] PlayerKnifeV —— knife V 键技能（BuildKnife.SecondarySkillIndex=2）
        // **依赖资产**（Resources 相对路径，缺失则 Cast 报 warning 不崩）：Character/Player/Skills/PlayerMelee|PlayerKnife|PlayerKnifeV.asset 均已有。
        // Add 顺序在 Move 之后、Gravity 之前——位移覆写 WishVelocity.xz 后由 Gravity 定 y。
        character.AddAfter<SkillCastComponent, MoveComponent>(new SkillCastComponent
        {
            SkillPaths = new List<string> { CharacterResPath.PlayerMelee, CharacterResPath.PlayerKnife, CharacterResPath.PlayerKnifeV },
            // HitLayers 默认全开。生产期建议改成只含敌人层。
        });
        // 连招前端：把左键(Light)/V(Heavy) 按当前武器的 ComboGraph 路由成连段，交 SkillCast 执行。
        // 无图的武器（枪 / 当前 knife 未配图）自动回退单招——接连招前的行为零改变。必须在 Input + SkillCast 之后 Add。
        character.AddAfter<ComboComponent, SkillCastComponent>(new ComboComponent());
        // 闪避：空格 → 按移动意图选 4 向翻滚 + 无敌帧。必须在输入组件 + Move 之后（位移覆写 WishVelocity.xz）、Gravity 之前。
        // 方向 clip 取自角色级 CharacterAnimSet（PlayerAnimSet 的 DodgeFwd/Bwd/Left/Right），无需额外资产路径。
        // 手感调校（距离 / 时长 / 位移曲线 / 无敌帧）全在 DodgeConfig 资产里，可在 Inspector 拖曲线调；
        // 资产缺失则用 DodgeComponent 内置默认（不崩）。见 CharacterResPath.PlayerDodgeConfig。
        character.AddAfter<DodgeComponent, ComboComponent>(new DodgeComponent
        {
            DodgeConfigPath = CharacterResPath.PlayerDodgeConfig,
        });
        // 重力 + 贴地。写 WishVelocity.y，放在所有写 x/z 的组件之后
        character.Add(new GravityComponent());
        character.Add(new HealthComponent
        {
            InitialMaxHealth = 100f,
        });
        // 反馈 + 清理：订阅 HealthComponent 事件。**多订阅者**的状态变化（damage 触发 flytext / 卡肉 / 自动清理）走事件，
        // 让 HealthComponent 只做 HP 数学 + 事件广播。单订阅者紧耦合的反馈（如近战 shake）直接放在主组件 inline 调 service。
        character.Add(new HitstopOnDamageComponent());
        character.Add(new AutoDespawnComponent { Delay = 3f });

        var view = manager.LoadBaseView<CharacterView>(CharacterResPath.PlayerPrefab, character);
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
        var character = new Character { TeamId = 2, CurrentCharacterAnimSetPath = CharacterResPath.PlayerAnimSet };
        // 给 Dummy 也装重力，spawn 后会被 Gravity + CC 一起拉到地面，避免悬空或半身埋在地形里
        character.Add(new GravityComponent());
        character.Add(new HealthComponent
        {
            InitialMaxHealth = maxHealth,
        });
        // Dummy 也接卡肉 + 自动清理（被打了也卡，死了 3s 后清）
        character.Add(new HitstopOnDamageComponent());
        character.Add(new AutoDespawnComponent { Delay = 3f });

        var view = manager.LoadBaseView<CharacterView>(CharacterResPath.PlayerPrefab, character);
        if (view != null)
        {
            view.gameObject.name = $"Dummy_{character.ID}";
            TeleportTo(view, position);
            // 同步 actor 状态：Bind 时已用 transform.position 写过一次，但是 instantiate 后我们才设位置，
            // 这里再回写一遍保证 character.Position 与 view 一致。
            character.Position = position;
        }

        // Dummy 没 WeaponComponent，不会触发 LoadWeaponAnimSet —— view.currentAnimSet 一直 null → DriveAnimation 早退 → 死了不播 Death。
        // 这里手动设默认 AnimSet（复用 Pistol 那套 clip），让 view 加载后 Die / Locomotion 都能跑
        character.CurrentWeaponAnimSetPath = "Weapon/Animations/Pistol"; // controller 轮询此路径变化即加载
        return character;
    }

    /// <summary>会动会打的僵尸敌人——**和玩家走同一套玩法管线**，只把输入源从 InputComponent 换成 <see cref="AIInputComponent"/>（巡逻→追击→攻击）：
    ///   AIInput 给世界空间移动意图 + 近身释放技能 → Aim（非瞄准→朝移动方向）+ Move（idle/慢走/快跑 locomotion）+ SkillCast（攻击/飞扑）。
    /// view 用专门的僵尸 prefab（Zombie 网格 + ZombieView + ZombieAnimancerController）。
    ///
    /// **依赖资产**（用菜单 Tools/TPS/Build Skill & Anim Assets 一键生成）：
    ///   - Resources/Character/Zombie/Prefabs/Zombie.prefab（MotusMan_v55 角色网格 + ZombieView/Animancer/CC，僵尸动画原生骨架）
    ///   - Resources/Character/Zombie/Animations/ZombieAnimSet.asset（CharacterAnimSet：Idle/Walk/Run + Death + 阈值）
    ///   - Resources/Character/Zombie/Skills/ZombieAttack.asset / ZombieLeap.asset（SkillDef）
    /// 资产缺失时：locomotion / 技能不播（graceful），AI 仍跑但看不到动作。
    ///
    /// 组件 Add 顺序：AIInput → Aim → Move → SkillCast → Gravity → Health → Hitstop → AutoDespawn
    ///   （AIInput 给意图；Aim 朝移动方向转身；Move 写 locomotion；SkillCast 释放时覆写 x/z；Gravity 定 y）。无 WeaponComponent（僵尸不持枪）。</summary>
    public Character CreateZombie(Vector3 position, float maxHealth = 200f)
    {
        var character = new Character { TeamId = 2, CurrentCharacterAnimSetPath = CharacterResPath.ZombieAnimSet };
        // 输入源：简单 AI（巡逻→5m 追玩家→2m 面向玩家随机放技能）。必须最先 Add（Aim/Move/Skill 在 Attach 里 Get 它）。
        character.Add(new AIInputComponent
        {
            DetectRange = 5f,
            AttackRange = 2f,
        });
        character.Add(new AimComponent());   // 不瞄准（AimHeld 恒 false）→ 朝 MoveWorld 转身
        character.Add(new MoveComponent
        {
            WalkSpeed = 0.2f,   // 慢走（对齐 ZombieAnimSet.WalkThreshold）
            SprintSpeed = 4f,   // 快跑（对齐 RunThreshold）
        });
        character.Add(new SkillCastComponent
        {
            SkillPaths = new List<string> { CharacterResPath.ZombieAttack, CharacterResPath.ZombieLeap },
            // HitLayers 默认全开（调试）。生产期设成只含玩家层。
        });
        character.Add(new GravityComponent());
        character.Add(new HealthComponent { InitialMaxHealth = maxHealth });
        character.Add(new HitstopOnDamageComponent());
        character.Add(new AutoDespawnComponent { Delay = 3f });

        // 用专门的僵尸 prefab（Zombie 模型 + ZombieView + ZombieAnimancerController）。
        // 由菜单 Tools/TPS/Build Skill & Anim Assets 从 Idle fbx 内嵌网格生成到 Resources/Character/Zombie/Prefabs/Zombie.prefab。
        var view = manager.LoadBaseView<ZombieView>(CharacterResPath.ZombiePrefab, character, addIfMissing: true);
        if (view != null)
        {
            view.gameObject.name = $"Zombie_{character.ID}";
            TeleportTo(view, position);
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
            ModelPath = "Weapon/Prefabs/RiflePlaceholder",
            // 临时复用 Pistol AnimSet（美工建好 RifleA.asset 后改成 "Weapon/Animations/RifleA"）
            AnimSetPath = "Weapon/Animations/Pistol",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            // 弹道起点（相对持有者 Position+Rotation 的本地坐标偏移）：玩家身高 1.2m + 朝前 0.6m 避开自己 capsule
            MuzzleLocalOffset = new Vector3(0f, 1.2f, 0.6f),
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
            FireInterval = 0.1f,
            Damage = new DamageSpec
            {
                BaseDamage = 25f,
                HitstopTier = HitstopTier.Short,  // 全自动 → Short 卡肉，避免每发都把目标钉死、节奏被毁
            },
            RecoilShakeIntensity = 0.08f, RecoilShakeDuration = 0.06f,  // 全自动小抖
            Effect = new LinearProjectileEffect { BulletSpeed = 80f, BulletLifetime = 2f },
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
            ModelPath = "Weapon/Prefabs/PistolPlaceholder",
            // 手枪用 Pistol 专属 WeaponAnimSet（Animancer 直接 Play 里面配的 clip）
            AnimSetPath = "Weapon/Animations/Pistol",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            // 弹道起点（相对持有者 Position+Rotation 的本地坐标偏移）：玩家身高 1.2m + 朝前 0.6m 避开自己 capsule
            MuzzleLocalOffset = new Vector3(0f, 1.2f, 0.6f),
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
            FireInterval = 0.3f,
            Damage = new DamageSpec
            {
                BaseDamage = 40f,
                HitstopTier = HitstopTier.Long,   // 半自动单发，每发 Long 卡肉给清晰命中反馈
            },
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
            ModelPath = "Weapon/Prefabs/RiflePlaceholder",
            // 临时复用 Pistol AnimSet（美工建好 MissileLauncher.asset 后改）
            AnimSetPath = "Weapon/Animations/Pistol",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            // 弹道起点（相对持有者 Position+Rotation 的本地坐标偏移）：玩家身高 1.2m + 朝前 0.6m 避开自己 capsule
            MuzzleLocalOffset = new Vector3(0f, 1.2f, 0.6f),
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
            FireInterval = 0.6f,
            Damage = new DamageSpec
            {
                BaseDamage = 80f,
                HitstopTier = HitstopTier.Long,   // 重武器爆破，Long 卡肉
            },
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

    /// <summary>突击步枪：连发风格 400 RPM，单发 35 伤，24 发弹匣。
    /// **示范多枪体系**：用独立 BurstRifle.asset（Shoot 用 Rifle_ShootBurst clip 跟 Pistol 的 ShootOnce 视觉明显不同）。
    /// 切到这把枪 view 自动加载 BurstRifle.asset → 上半身播 burst 风格开火；下半身 locomotion 不被打断（CharacterAnimSet 保持）。</summary>
    private static Weapon BuildBurstRifle()
    {
        var w = new Weapon
        {
            Name = "Burst Rifle",
            ModelPath = "Weapon/Prefabs/RiflePlaceholder",
            // 用专属 AnimSet——演示切武器时上半身动画切换（下半身保持 PlayerAnimSet locomotion）
            AnimSetPath = "Weapon/Animations/BurstRifle",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = Vector3.zero,
            MuzzleLocalOffset = new Vector3(0f, 1.2f, 0.6f),
            BackLocalPosition = new Vector3(0f, 0.15f, -0.2f),
            BackLocalEuler = new Vector3(0f, 90f, 0f),
            MagCapacity = 24,
            CurrentAmmo = 24,
            HeavyRecoil = false,
            RecoilAnimSpeed = 2f,        // ShootBurst clip 偏长，加速到 0.4s 内播完匹配 FireInterval
        };
        w.Add(new FireComponent
        {
            FireInterval = 0.4f,         // 400 RPM 连发
            Damage = new DamageSpec
            {
                BaseDamage = 35f,
                HitstopTier = HitstopTier.Long,  // 单发伤害高 → 长卡肉
            },
            RecoilShakeIntensity = 0.25f, RecoilShakeDuration = 0.15f,
            Effect = new LinearProjectileEffect { BulletSpeed = 70f, BulletLifetime = 2f },
        });
        w.Add(new ReloadComponent { ReloadDuration = 1.8f });
        return w;
    }

    /// <summary>近战 knife：**纯技能武器**——不装 FireComponent / ReloadComponent，所以左键不开火、R 不换弹。
    /// 左键（OnFireDown）放技能 1、V 键放技能 2（PrimarySkillIndex/SecondarySkillIndex 指向玩家 SkillCastComponent.SkillPaths 的下标）。
    /// 上身持刀 pose 走 WeaponAnimSet（IdleGunPose）；两个技能动作走 SkillDef（全身），跟枪的开火/换弹无关。
    ///
    /// **依赖资产**（均已就绪）：
    ///   - Weapon/Prefabs/knife.prefab（刀模型，mesh-only）
    ///   - Weapon/Animations/Knife.asset（WeaponAnimSet：IdleGunPose/AimPose=持刀待机 + Equip/Holster；无 Shoot/Reload）
    ///   - Character/Player/Skills/PlayerKnife.asset（SkillDef）= 左键技能（SkillPaths[1]）
    ///   - Character/Player/Skills/PlayerKnifeV.asset（SkillDef）= V 键技能（SkillPaths[2]）</summary>
    private static Weapon BuildKnife()
    {
        return new Weapon
        {
            Name = "Knife",
            ModelPath = "Weapon/Prefabs/knife",
            AnimSetPath = "Weapon/Animations/Knife",
            HandLocalPosition = Vector3.zero,
            HandLocalEuler = new Vector3(150f, 10f, 0f),   // 刀握持朝向
            BackLocalPosition = new Vector3(0f, 0.15f, -0.2f),
            BackLocalEuler = new Vector3(0f, 90f, 0f),
            // 近战不开火、不换弹：无 FireComponent / ReloadComponent，无弹药概念
            MagCapacity = 0,
            CurrentAmmo = 0,
            // 刀单独配完整的拔刀/收刀过场（枪走全局快切 0.7/1.3，会截短；刀要播完整 1.8s clip）。
            // EquipRifle/HolsterRifle = 54帧@30fps ≈ 1.8s。想更快可加大 SwapAnimSpeed（动画仍完整），如 1.8→各约1秒。
            HolsterDuration = 1.8f,
            EquipDuration = 1.8f,
            SwapAnimSpeed = 1f,
            // 左键→技能 PlayerKnife（SkillPaths[1]）；V→技能 PlayerKnifeV（SkillPaths[2]）。
            // 这两条是**无连招图时的单招回退**——配了 ComboGraphPath 后走连招、忽略这两个下标。
            PrimarySkillIndex = 1,
            SecondarySkillIndex = 2,
            // 连招图（菜单 Tools/TPS/Build Combo Demo (Knife) 一键生成）：左键三连段 + 重击分支。
            // 资产缺失（没跑生成器）→ ComboComponent 回退到上面 Primary/Secondary 单招（仅一条 warning，不崩）。
            ComboGraphPath = CharacterResPath.KnifeCombo,
        };
    }
}
