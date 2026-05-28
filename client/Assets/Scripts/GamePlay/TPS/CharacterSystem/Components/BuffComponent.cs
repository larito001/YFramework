using UnityEngine;

/// <summary>
/// Buff 容器组件（**当前是占位**）：接收 <see cref="HealthComponent"/> 命中后转交的 <see cref="BuffSpec"/>，
/// 未来管理 buff timer / 叠加层数 / 减伤 / 修改属性 / 触发 OnTick 效果等。
///
/// **协议（已就位，未来扩展按此协议实现）**：
///   - <see cref="AddBuff"/> 是唯一入口。任何"加 buff" 的操作（命中携带 / 技能主动施加 / 区域光环）都走这里
///   - DamageInfo.AppliedBuffs 已经在 HealthComponent.ApplyDamage 命中后调本方法
///   - 当前实现只 log，buff 不实际生效——未来加 Tick 推进 timer / 移除过期 buff / 触发减伤 / DOT 等
///
/// 为何先放 ICharacterComponent 不是 IActorComponent：buff 一般跟 Character 强绑（动画 / UI 显示 / 复杂逻辑），
/// 未来如果塔 / 子弹也需要 buff，再升级 IActorComponent。
/// </summary>
public class BuffComponent : ICharacterComponent
{
    /// <summary>添加一个 buff。当前占位实现只 log——未来加 buff 列表管理、timer、stack、减伤、属性修改等。
    /// 调用方（HealthComponent 命中链 / 技能施法 / 光环）只调本方法，不该绕开直接操作 buff 列表。</summary>
    public void AddBuff(BuffSpec spec)
    {
        if (spec.BuffId <= 0) return;  // 无效 buff 跳过
#if UNITY_EDITOR
        Debug.Log($"[Buff] actor={Owner?.ID} +BuffId={spec.BuffId} dur={spec.Duration:F1}s (placeholder, no effect)");
#endif
        // TODO: 真实现时——加到内部 List<BuffInstance>，每帧 Tick 推 timer，到期触发 OnRemoved 移除
    }
}
