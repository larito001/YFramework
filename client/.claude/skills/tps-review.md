---
name: tps-review
description: Use this skill when the user asks to review TPS code (e.g. "review the diff", "tps-review", "check TPS"), or before committing a TPS-related change. Checks boundary + lifecycle issues in Assets/Scripts/GamePlay/TPS/ per ARCHITECTURE.md. Outputs a tight violations list — nothing else.
---

针对 `Assets/Scripts/GamePlay/TPS/` 的 diff 做边界 + 生命周期检查。规范见 `Assets/Scripts/GamePlay/TPS/ARCHITECTURE.md`。

# 流程

1. `git diff main...HEAD` 拿改动。用户传了 base 就用 base，否则默认 `main`。
2. 只看 `Assets/Scripts/GamePlay/TPS/` 下的改动行（含新增文件）。其他目录跳过。
3. 对每个改动文件按下面 7 条 grep + 读上下文。
4. 输出违规。没违规一行 `No issues found.`。

# 7 条检查

## 分层（看到就标）

1. **逻辑组件直接驱动状态变化的派生反馈**
   红线：`HealthComponent.ApplyDamage` 等"状态变化触发点"里 inline 调 `FlyTextMgr` / `TimeScaleService` / `CharacterManager.Remove*` / `UIMgr` 等——这种"多订阅者关心同一个状态变化"的场景必须走事件。
   例外（**OK 不报**）：
     - 通过订阅事件再调 service 的"订阅组件"（如 `HitstopOnDamageComponent` 订阅 `OnDamaged`）
     - **动作伴随的瞬时反馈**：相机抖屏（`cameraMgr.Shake.Shake(...)`）、播音效、粒子触发——这些是动作本身的一部分，单一紧耦合，inline OK
   判断："这件事会有多个订阅者吗 / 是状态变化的衍生吗" 是→必须事件，否→inline 可。
   修：状态衍生反馈拆订阅组件或挪到 view。

2. **view 写意图字段**
   `XxxView.cs` 里 `character.WishVelocity = ` / `character.IsShooting = ` / `character.AnimMoveX = ` / `character.Rotation = ` 等对意图字段的赋值。
   例外：物理回写（`Position` / `IsGrounded`）、消费一次性 trigger（`character.Reload = false`）。
   修：意图字段只能组件写。

3. **跨 actor / 跨 view 持裸引用**
   组件字段含 `Character`/`Weapon`/`Bullet` 类型（不是 Owner）；view 字段含别的 `XxxView` 类型。
   修：存 ID + `ActorWorld.Get<T>(id)` / `ViewManager.TryGetView(id)`。

4. **GameObject.Find / FindObjectOfType / FindGameObjectWithTag**
   grep 出来即报。

## 生命周期

5. **Detach 漏清自己写过的字段**
   对每个组件：列 Tick 里写 Owner 的字段名，对照 Detach 是否清回默认。
   例外：注释明说"持久状态"的（`Position` / `CurHealth` / `IsDead` 等）。

6. **事件订阅没退订**
   `event += handler` 在 Attach 出现，必须有 `event -= handler` 在 Detach。

7. **遍历中改 list 元素**
   `for (int i; i < list.Count; i++)` 循环体里直接 `list.Remove` / `RemoveAt`，没走 `toRemove` 暂存模式。
   参考 `BulletManager.Tick` / `CharacterManager.Tick` 的延迟移除。

# 输出格式

每条一行：

```
🔴 [<类>] <文件>:<行号> — <一句话理由>。
  修：<一句话修法>。
```

类 ∈ `boundary` / `lifecycle`。

无违规：单行输出 `No issues found.`。

# 不要

- 不要批评命名 / 风格 / 注释多寡。
- 不要建议"或许更好"的重构。
- 不要逐文件输出"已检查"。
- 不要建议加测试 / 加文档。
- 只报真违反 7 条之一的问题。
