using UnityEngine;

/// <summary>
/// **玩家**动画驱动器。继承 <see cref="AnimConductor"/>（locomotion / death / 分层 / 技能全身覆盖），
/// 在此实现**武器上身**分层（下身 locomotion 含瞄准 2D strafe 已上移到基类 <see cref="LocomotionDriver"/>，本类不再管下身）：
///   - <see cref="WeaponAnimSet"/> 加载（**仅上身** combat/持枪 pose 跟武器走，切枪时换；Tick 里轮询 character.CurrentWeaponAnimSetPath 变化自治加载）
///   - **上身（Layer 1 mask）**：**全部委托给 <see cref="UpperBodyLayerDriver"/>**——持武器常驻持枪/瞄准 pose + 换弹/后坐力/拿出/收回 one-shot。
///     本类的上身钩子（HasUpperBodyBasePose / UpdateUpperBody / EnterFullBodyOverride / RestoreUpperBodyAfterFullBody）都是转发给 driver 的薄封装。
///   - 武器未配任一持枪 pose 时 driver 不接管，退化为旧式 Layer 1 one-shot（播完淡出整层，基类 3c）。
///
/// 全身覆盖（Die / 技能）在基类；进出全身覆盖经 EnterFullBodyOverride / RestoreUpperBodyAfterFullBody 钩子，由 driver 干净接管上身常驻。
/// </summary>
public class CharacterAnimancerController : AnimConductor
{
    // ── WeaponAnimSet（跟武器走）──
    private WeaponAnimSet weaponAnimSet;

    // ── 上身层驱动（Layer 1 的全部持武器逻辑收口在这里；仅 useUpperBodyLayer 时 new 出来）──
    private UpperBodyLayerDriver upperBody;

    /// <summary>上次加载的 weapon AnimSet 路径——轮询 <see cref="Character.CurrentWeaponAnimSetPath"/> 变化触发重载
    /// （替代旧 WeaponAnimDirty 一次性 trigger，与 ComboComponent 轮询 CurrentComboGraphPath 同款）。</summary>
    private string loadedWeaponAnimSetPath;

    public override void Dispose()
    {
        weaponAnimSet = null;
        loadedWeaponAnimSetPath = null;
        upperBody?.Dispose();
        upperBody = null;
        base.Dispose();
    }

    /// <summary>切 WeaponAnimSet：**轮询 <see cref="Character.CurrentWeaponAnimSetPath"/> 变化即重载**
    /// （替代旧 WeaponAnimDirty trigger，与 ComboComponent 轮询 CurrentComboGraphPath 同款）+ 同步上身层启用。
    /// 下身 aim mixer 不动（角色级，跟 CharacterAnimSet 走）。</summary>
    protected override void PreDrive(Character character)
    {
        if (character.CurrentWeaponAnimSetPath != loadedWeaponAnimSetPath)
        {
            loadedWeaponAnimSetPath = character.CurrentWeaponAnimSetPath;
            LoadWeaponAnimSet(character.CurrentWeaponAnimSetPath);
            upperBody?.SetWeapon(weaponAnimSet);
            if (!layer0FullBodyActive) upperBody?.SyncActivation(); // 全身覆盖中不升起（由 Restore 接管）
        }
    }

    // ────────────────── 上身层钩子（全部转发给 UpperBodyLayerDriver，分层逻辑集中在那里） ──────────────────

    /// <summary>持武器且配了持枪 pose 时，由 driver 接管常驻上身（基类据此跳过退化 one-shot 生命周期）。</summary>
    protected override bool HasUpperBodyBasePose(Character character) => upperBody != null && upperBody.HasPose;

    /// <summary>玩家初始无武器：Layer 1 静默（weight 0），由首次装备的 driver.SyncActivation 升起。</summary>
    protected override float GetInitialUpperBodyWeight() => 0f;

    /// <summary>进入全身覆盖（死亡/技能）：让上身层让位——driver 独占 Layer 1，降 weight 静默 + 清常驻缓存（保证技能后干净重建）。</summary>
    protected override void EnterFullBodyOverride(float fade, bool immediate)
    {
        if (upperBody != null) upperBody.Silence(fade, immediate); // driver 独占 Layer 1 weight + 清缓存
        else base.EnterFullBodyOverride(fade, immediate);          // 无 driver（无 mask）走基类通用静默
    }

    /// <summary>技能结束从全身覆盖恢复：driver 把 Layer 1 weight 拉回 1 + 当帧重建 base pose（按当前 IsAiming）。</summary>
    protected override void RestoreUpperBodyAfterFullBody(Character character, float recoverFade)
        => upperBody?.Restore(recoverFade);

    /// <summary>每帧驱动上身常驻状态机（base pose + 叠加 one-shot）。</summary>
    protected override void UpdateUpperBody(Character character) => upperBody?.Tick(character);

    /// <summary>武器战斗段。持武器且配了 pose 时短路（trigger 交给 driver.Tick 消费）；
    /// 退化路径（无 pose）沿用旧式 Layer 1 / 单层 one-shot（基类 3c 淡出整层）。无武器仅消费 trigger 防重复。</summary>
    protected override bool DriveCombat(Character character, float fade)
    {
        if (weaponAnimSet == null)
        {
            character.ClearCombatOneShots(); // 没 weaponAnimSet：清空挂起 combat one-shot 避免堆积（取代旧版逐个 bool 手动清）
            return false;
        }

        // 持武器且配了 pose：trigger 由 driver 消费（HasUpperBodyBasePose 为真 -> 基类走 UpdateUpperBody），这里不碰
        if (HasUpperBodyBasePose(character)) return false;

        // 退化路径（武器未配 pose）：复用 driver 的静态 trigger 消费，在 combatLayer 播 one-shot，基类 3c 负责淡出
        if (UpperBodyLayerDriver.TryConsumeCombatTrigger(character, weaponAnimSet, GetDefaultFade(),
                out var clip, out var f, out var speed) && clip != null)
        {
            // 退化模式 Layer 1 初始 weight=0（玩家），播 one-shot 前拉起，基类 3c 播完淡回 0
            if (HasUpperLayer) UpperLayer.StartFade(1f, f);
            var combatLayer = useUpperBodyLayer ? UpperLayer : BaseLayer;
            var s = combatLayer.Play(clip, f);
            if (s != null && speed > 0f) s.Speed = speed;
            activeOneShotState = s;
        }
        return false;
    }

    /// <summary>加载 WeaponAnimSet（切枪时换，仅上身）。aim strafe mixer 是角色级（CharacterAnimSet），切枪不重建。</summary>
    private void LoadWeaponAnimSet(string path)
    {
        weaponAnimSet = null;
        // 不动下半身 locomotion（1D/aim mixer 在基类 LocomotionDriver，跟 CharacterAnimSet 走、切枪不重建）、activeOneShotState
        // ——切枪不该打断下半身 locomotion，combat trigger 自然下一帧覆盖

        if (string.IsNullOrEmpty(path)) return;
        if (resMgr == null) return;
        var set = resMgr.Load<WeaponAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[CharacterAnimancerController] WeaponAnimSet 加载失败: {path}");
            return;
        }

        weaponAnimSet = set;
    }

    /// <summary>CharacterAnimSet 加载完（spawn 一次）：创建上身层驱动（仅配了 UpperBodyMask 时）。下身 aim 2D mixer 已由基类 LocomotionDriver 构造。</summary>
    protected override void OnCharacterAnimSetLoaded()
    {
        upperBody = HasUpperLayer
            ? new UpperBodyLayerDriver(UpperLayer, characterAnimSet.UpperBodyMask, GetDefaultFade())
            : null;
    }
}
