using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持枪人组件（Character 侧，纯逻辑）：
///   - 持有武器槽位 Weapon Actor 列表，Attach 时交给 WeaponManager 接管（注册 + 创建 view）
///   - 切枪：订阅 InputComponentBase.OnWeaponSelect（1~9 数字键）→ Equip(slot) → 走 Holster→Equip 两阶段过场
///   - 射击：每帧把 input.FireHeld 写到 Owner.IsShooting（开火行为由 Weapon 的 FireComponent 自己消费）
///   - 换弹：订阅 InputComponentBase.OnReload → 转发给 currentWeapon.ReloadRequest，<see cref="ReloadComponent"/> 自己处理。
///     本组件镜像 currentWeapon.IsReloading → Owner.IsReloading（动画门控），上升沿触发 Owner.Reload trigger。
///
/// 近战已上移为通用"技能"（<see cref="SkillDef"/> + <see cref="SkillCastComponent"/>）。本组件只在 Tick 里**读**
/// <see cref="Character.IsCastingSkill"/> 做开火/换弹/切枪门控（技能释放途中不可被这些操作打断），不写入技能字段。
///
/// 字段写入：FireOrigin / FireDirection / FireTarget / FireIntent → currentWeapon。
/// IsShooting / IsSwapping / WeaponSwap / WeaponHolster / CurrentWeaponSlot / IsReloading / Reload / Shoot / HeavyRecoil / RecoilAnimSpeed → Owner。
/// </summary>
public class WeaponComponent : ICharacterComponent
{
    public List<Weapon> Weapons = new List<Weapon>();
    public int InitialSlot = 0;
    /// <summary>武器挂手部 socket 的骨骼名。RifleAnimsetPro 是 RightHandProp。</summary>
    public string SocketName = "RightHandProp";
    /// <summary>武器挂背部 socket 的骨骼名。空字符串 = 切枪过场期间不挂背（武器会因 IsEquipped 切换瞬间不可见，
    /// 等 MountToHandDelay 到点直接出现在手里）。RifleAnimsetPro 默认 rig 没有专门的"背"骨，
    /// 用 Spine1 凑（位置/朝向需要每把武器在 Factory 里调 BackLocalPosition/BackLocalEuler）。</summary>
    public string BackSocketName = "Spine1";
    /// <summary>Equip（取出新枪）阶段**默认**时长（秒）。枪走快切手感（短于 1.8s 的 Equip clip，取出动画会被截短——枪可接受）。
    /// 想播完整 clip（如近战刀）：在 Weapon.EquipDuration 按武器覆盖成 clip 长度（EquipRifle=54帧@30fps≈1.8s），或调 Weapon.SwapAnimSpeed。
    /// 实际过场 = (Weapon.EquipDuration 或本默认) / Weapon.SwapAnimSpeed。</summary>
    public float WeaponSwapDuration = 1.3f;
    /// <summary>Holster（收回旧枪）阶段**默认**时长（秒）。枪走快切手感（短于 Holster clip，收回动画会被截短——枪可接受）。
    /// 切枪总锁开火时长 = HolsterDuration + WeaponSwapDuration。Holster 阶段结束后才把新武器挂到背上并触发 Equip 动画。
    /// 想播完整 clip：Weapon.HolsterDuration 覆盖成 clip 长度（≈1.8s），或调 Weapon.SwapAnimSpeed。</summary>
    public float HolsterDuration = 0.7f;
    /// <summary>Equip 阶段开始后多少秒把新武器 reparent 到手（"抽枪到位"那一刻）。
    /// 默认 0.5s = EquipRifle 抽枪到手大约的时机。&lt;=0 立即挂手（关闭过场效果）。</summary>
    public float MountToHandDelay = 0.5f;

    private InputComponentBase input;
    private WeaponManager weaponMgr;
    private Weapon currentWeapon;
    private float swapLockTimer;
    private bool prevReloading;     // 上升沿检测：currentWeapon.IsReloading 从 false→true 时触发 Owner.Reload 一次性 trigger
    private float mountToHandTimer; // >0 时 currentWeapon 还挂在背上，到点 Mount 到手部
    private Weapon pendingHandMount; // mountToHandTimer 到点时要挂手的武器（= currentWeapon，但显式存避免误读）
    private float holsterTimer;     // >0 时 Holster 阶段进行中，旧武器还在手里、播放 HolsterRifle
    private int pendingSwapSlot = -1; // holsterTimer 到点时要切到的新武器槽

    public override void Attach(Character owner)
    {
        if (Ctx == null) { Debug.LogError("[WeaponComponent] GameLoop.Ctx 未就绪"); return; }
        Ctx.TryGet(out weaponMgr);
        // 从同 Actor 的输入组件读意图（不直接碰 InputService）
        input = owner.Get<InputComponentBase>();
        if (input != null)
        {
            input.OnWeaponSelect += HandleWeaponSelect;
            input.OnReload += HandleReload;
        }
        // 把配置中的 Weapon Actor 实例交给 WeaponManager（注册 + 创建 view）
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Adopt(Weapons[i]);
        }
        // 初始装备：不播切枪动画
        EquipInternal(InitialSlot, playAnim: false);
    }

    public override void Detach()
    {
        if (input != null)
        {
            input.OnWeaponSelect -= HandleWeaponSelect;
            input.OnReload -= HandleReload;
        }
        // 武器随持枪人一起销毁
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Despawn(Weapons[i]);
        }
        // 清自己写过的 Owner 状态字段。技能相关字段由 SkillCastComponent 负责。
        if (Owner != null)
        {
            Owner.IsShooting = false;
            Owner.IsSwapping = false;
            Owner.WeaponSwap = false;
            Owner.WeaponHolster = false;
            Owner.IsReloading = false;
            Owner.Reload = false;
            Owner.Shoot = false;
            // "持有武器属性"字段：无 WeaponComponent = 无武器，回默认。
            // -1 = 无武器槽（默认 0 是合法槽，留 0 让 HUD 误以为还持槽 0）；HeavyRecoil/RecoilAnimSpeed 回 Animator 默认。
            Owner.CurrentWeaponSlot = -1;
            Owner.HeavyRecoil = false;
            Owner.RecoilAnimSpeed = 1f;
            Owner.SwapAnimSpeed = 1f;
            Owner.WeaponPrimarySkill = -1;
            Owner.WeaponSecondarySkill = -1;
            Owner.CurrentComboGraphPath = null;
            // 清动画 set 链：触发 view 回 idle pose
            Owner.CurrentWeaponAnimSetPath = null;
            Owner.WeaponAnimDirty = true;
        }
        currentWeapon = null;
        pendingHandMount = null;
        input = null;
        weaponMgr = null;
        swapLockTimer = 0f;
        mountToHandTimer = 0f;
        holsterTimer = 0f;
        pendingSwapSlot = -1;
        prevReloading = false;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;
        // 死亡时打断换弹 / 清射击 / 清切枪状态。
        // 写 currentWeapon.IsReloading=false 后 ReloadComponent 下一帧看到就跳过 timer（外部打断协议）。
        // 切枪相关 timer 和标志也一并清掉：本分支提前 return 不再 decrement timer，
        // 不清的话 IsSwapping 会永远卡 true，未来若加复活组件会无法再开火/近战/换弹。
        if (Owner.IsDead)
        {
            if (currentWeapon != null && currentWeapon.IsReloading) currentWeapon.IsReloading = false;
            Owner.IsReloading = false;
            Owner.IsShooting = false;
            Owner.IsSwapping = false;
            Owner.WeaponSwap = false;
            Owner.WeaponHolster = false;
            swapLockTimer = 0f;
            holsterTimer = 0f;
            mountToHandTimer = 0f;
            pendingSwapSlot = -1;
            pendingHandMount = null;
            prevReloading = false;
            return;
        }

        // 镜像 currentWeapon.IsReloading → Owner.IsReloading（view 动画门控）。
        // 上升沿（false→true）→ 一次性 Owner.Reload trigger，view 消费 SetTrigger("Reload")。
        bool currReloading = currentWeapon != null && currentWeapon.IsReloading;
        Owner.IsReloading = currReloading;
        if (currReloading && !prevReloading) Owner.Reload = true;
        prevReloading = currReloading;

        // 开火条件：瞄准 + 没在切枪 + 没在近战/闪避 + 没在换弹 + 没死 + 有弹（MagCapacity=0 是无限弹药武器，跳过弹药门控）
        //   + 非近战武器（WeaponPrimarySkill<0；近战/技能武器左键放技能，永不开火）
        bool hasAmmo = currentWeapon == null || currentWeapon.MagCapacity <= 0 || currentWeapon.CurrentAmmo > 0;
        Owner.IsShooting = input.FireHeld && Owner.IsAiming && !Owner.IsSwapping && !Owner.IsBusy && !Owner.IsReloading && !Owner.IsDead && hasAmmo && Owner.WeaponPrimarySkill < 0;

        // 射击一次性 trigger 镜像：FireComponent 每发射成功置 ShootEvent，view 端 SetTrigger("Shoot") 重启 Recoil 动画
        if (currentWeapon != null && currentWeapon.ShootEvent)
        {
            currentWeapon.ShootEvent = false;
            Owner.Shoot = true;
        }

        // 当前武器的开火意图 + 弹道源：FireComponent 自己消费（射速/弹夹/扩散在子组件里再叠）
        // origin 由武器侧配（currentWeapon.MuzzleLocalOffset），WeaponComponent 只机械应用：
        //   FireOrigin = holder.Position + holder.Rotation * MuzzleLocalOffset
        // 不同武器枪口长度不一样、避自身 capsule 距离也不一样——配置归武器，玩家持枪人不操心。
        // direction = AimTargetWorldPos - origin：AimComponent 已经把鼠标投影到同一枪口高度平面，dir 天然水平
        if (currentWeapon != null)
        {
            currentWeapon.FireIntent = Owner.IsShooting;
            var origin = Owner.Position + Owner.Rotation * currentWeapon.MuzzleLocalOffset;
            var characterForward = Owner.Rotation * Vector3.forward;

            // 鼠标停在自己身上 / 离 muzzle 太近 / 落在角色背后时，aim-origin 会指向自己（甚至反向），
            // 子弹会撞进自己 CharacterController。三道兜底：水平面化、距离阈值、与 forward 同向校验，
            // 任一失败就回退到 character.forward。
            var dir = Owner.AimTargetWorldPos - origin;
            dir.y = 0f;
            const float minAimDistSqr = 0.25f; // 0.5m 之内的瞄准点忽略，强制 forward
            if (dir.sqrMagnitude < minAimDistSqr || Vector3.Dot(dir, characterForward) <= 0f)
            {
                dir = characterForward;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; // 极端情况兜底
            dir.Normalize();

            currentWeapon.FireOrigin = origin;
            currentWeapon.FireDirection = dir;
            currentWeapon.FireTarget = Owner.AimTargetWorldPos;
        }

        // 切枪锁开火计时
        if (swapLockTimer > 0f)
        {
            swapLockTimer -= dt;
            if (swapLockTimer <= 0f) Owner.IsSwapping = false;
        }

        // Holster 阶段倒计时：旧武器还挂在手里播 HolsterRifle，到点真正执行 swap（unmount 旧 + 挂背新 + 播 EquipRifle）
        if (holsterTimer > 0f)
        {
            holsterTimer -= dt;
            if (holsterTimer <= 0f)
            {
                holsterTimer = 0f;
                bool mountToBack = MountToHandDelay > 0f && !string.IsNullOrEmpty(BackSocketName);
                ApplySwap(pendingSwapSlot, playEquipAnim: true, mountToBack: mountToBack);
                pendingSwapSlot = -1;
            }
        }

        // "枪从背切到手"倒计时：Equip 阶段开始时枪挂在背上（或隐藏），动画播一半到点再 reparent 到手部 socket。
        if (mountToHandTimer > 0f)
        {
            mountToHandTimer -= dt;
            if (mountToHandTimer <= 0f)
            {
                mountToHandTimer = 0f;
                if (pendingHandMount != null && weaponMgr != null)
                    weaponMgr.Mount(pendingHandMount, Owner, SocketName);
                pendingHandMount = null;
            }
        }
    }

    /// <summary>切到指定槽位。槽越界静默忽略。只走 WeaponManager Mount/Unmount，view 自己响应。</summary>
    public void Equip(int slot) => EquipInternal(slot, playAnim: true);

    /// <summary>切槽入口。playAnim=true 走 Holster→Equip 两阶段过场（旧枪先收回背，新枪再抽出来）；
    /// playAnim=false 或没有 currentWeapon 可收，跳过过场直接挂手（用于初始装备 / 直接强制切换）。
    /// 切枪期间二次调用会被忽略，防止 mid-swap 状态错乱。</summary>
    private void EquipInternal(int slot, bool playAnim)
    {
        if (Owner == null) return;
        if (Weapons == null || slot < 0 || slot >= Weapons.Count) return;

        // 死亡 / 技能释放 / 闪避中静默忽略（手部 / 全身被占用）。playAnim=false 分支是 Factory 强制 spawn 路径，不走这两条门控。
        // **不**拦 IsReloading：换弹中切枪打断 reload 是设计内的（ApplySwap 主动清旧武器 IsReloading=false）。
        if (playAnim && Owner.IsDead) return;
        if (playAnim && Owner.IsBusy) return;

        // mid-swap 守卫：动画切枪期间禁止再切。用 Owner.IsSwapping（由 swapLockTimer 覆盖全 Holster+Equip 时长驱动）
        // 比 holsterTimer/mountToHandTimer 更严密 —— 当 BackSocketName 空或 MountToHandDelay<=0 时两个 timer 可能没启动，
        // 但 IsSwapping 始终覆盖整段过场。
        if (playAnim && Owner.IsSwapping) return;

        bool slotChanged = Owner.CurrentWeaponSlot != slot;

        // 需要过场动画：playAnim + slotChanged + 有当前武器可 holster
        if (playAnim && slotChanged && currentWeapon != null)
        {
            pendingSwapSlot = slot;
            Owner.WeaponHolster = true;
            Owner.IsSwapping = true;
            // 过场时长 = clip 自然长度 / SwapAnimSpeed（动画完整播完、速度由武器配置定）。
            //   Holster 跟"被收起的旧武器"走、Equip 跟"取出的新武器"走；时长 <0 回退组件默认，速度 <=0 视作 1。
            var newWeapon = Weapons[slot];
            float holsterBase = currentWeapon.HolsterDuration >= 0f ? currentWeapon.HolsterDuration : HolsterDuration;
            float holsterSpeed = currentWeapon.SwapAnimSpeed > 0f ? currentWeapon.SwapAnimSpeed : 1f;
            float equipBase = (newWeapon != null && newWeapon.EquipDuration >= 0f) ? newWeapon.EquipDuration : WeaponSwapDuration;
            float equipSpeed = (newWeapon != null && newWeapon.SwapAnimSpeed > 0f) ? newWeapon.SwapAnimSpeed : 1f;
            float holsterDur = holsterBase / holsterSpeed;
            float equipDur = equipBase / equipSpeed;
            holsterTimer = holsterDur;
            swapLockTimer = holsterDur + equipDur;
            return;
        }

        // 不走过场：直接挂手（initial spawn / 强制 / 无旧枪可收）
        ApplySwap(slot, playEquipAnim: false, mountToBack: false);
    }

    /// <summary>实际切枪动作：unmount 旧武器 + 挂新武器（到背或到手）+ 写后坐力参数 + 可选触发 Equip 动画。
    /// 被 EquipInternal 的"无过场"分支直接调；被 Tick 里 Holster 倒计时到点调（开 Equip 阶段）。
    /// 换枪打断旧武器的 reload：写旧 currentWeapon.IsReloading=false，ReloadComponent 下一 Tick 看到就跳过 timer。</summary>
    private void ApplySwap(int slot, bool playEquipAnim, bool mountToBack)
    {
        if (Owner == null) return;
        if (Weapons == null || slot < 0 || slot >= Weapons.Count) return;
        var w = Weapons[slot];
        bool slotChanged = Owner.CurrentWeaponSlot != slot;
        Owner.CurrentWeaponSlot = slot;

        // 换枪打断旧武器的 reload（避免切回去时旧武器还在 reload，或者 timer 在后台继续到点回填）
        if (slotChanged && currentWeapon != null && currentWeapon.IsReloading)
        {
            currentWeapon.IsReloading = false;
        }

        if (weaponMgr != null)
        {
            if (currentWeapon != null && currentWeapon != w) weaponMgr.Unmount(currentWeapon);
            if (w != null)
            {
                if (mountToBack) weaponMgr.MountOnBack(w, Owner, BackSocketName);
                else weaponMgr.Mount(w, Owner, SocketName);
            }
        }
        currentWeapon = w;
        prevReloading = currentWeapon != null && currentWeapon.IsReloading; // 重置上升沿基线，避免切到一个正在 reload 的武器误触发 Reload trigger

        // 把新武器的后坐力动画参数推给 Owner，view 每帧 SetBool/SetFloat 给 Animator
        if (currentWeapon != null)
        {
            Owner.HeavyRecoil = currentWeapon.HeavyRecoil;
            Owner.RecoilAnimSpeed = currentWeapon.RecoilAnimSpeed;
            // 取出/收回速度倍率：driver 据此设 Equip/Holster one-shot 的 Speed（过场时长在 EquipInternal 已同步缩放）
            Owner.SwapAnimSpeed = currentWeapon.SwapAnimSpeed > 0f ? currentWeapon.SwapAnimSpeed : 1f;
        }
        // 武器的技能映射推给 Owner（ComboComponent 据此回退单招、近战武器免开火）。无武器回 -1。
        Owner.WeaponPrimarySkill = currentWeapon?.PrimarySkillIndex ?? -1;
        Owner.WeaponSecondarySkill = currentWeapon?.SecondarySkillIndex ?? -1;
        // 武器的连招图路径推给 Owner（ComboComponent 轮询变化重载图）。空 = 该武器无连招 → ComboComponent 回退单招。
        Owner.CurrentComboGraphPath = currentWeapon?.ComboGraphPath;

        // 通知 view 切 WeaponAnimSet（path 空 = 回退默认 idle pose）
        // CharacterView 检测 WeaponAnimDirty trigger 后 ResMgr.Load<WeaponAnimSet> + Animancer.Play 替代原 Animator state
        Owner.CurrentWeaponAnimSetPath = currentWeapon?.AnimSetPath;
        Owner.WeaponAnimDirty = true;

        if (playEquipAnim)
        {
            Owner.WeaponSwap = true;
            if (mountToBack && MountToHandDelay > 0f)
            {
                pendingHandMount = currentWeapon;
                // 挂手时机也按取出速度缩放（Equip 动画快了，抽枪到手也提前）
                float swapSpeed = currentWeapon != null && currentWeapon.SwapAnimSpeed > 0f ? currentWeapon.SwapAnimSpeed : 1f;
                mountToHandTimer = MountToHandDelay / swapSpeed;
            }
        }
        // 注：IsSwapping / swapLockTimer 在 EquipInternal 启动阶段就设好覆盖 Holster+Equip 总时长，这里不重写
    }

    private void HandleWeaponSelect(int slot) => Equip(slot);

    /// <summary>R 键事件：校验后转发请求给 currentWeapon，ReloadComponent 自己处理 timer + 弹匣回填。</summary>
    private void HandleReload()
    {
        if (Owner == null || Owner.IsDead) return;
        if (currentWeapon == null) return;
        // 切枪/技能释放/闪避中不响应：这些状态下角色手是占用的
        if (Owner.IsSwapping || Owner.IsBusy) return;
        currentWeapon.ReloadRequest = true;
    }
}
