---
name: tps-review
description: Use this skill when the user asks to review TPS code (e.g. "review the diff", "tps-review", "check TPS"), or before committing a TPS-related change. Checks boundary + lifecycle issues in Assets/Scripts/GamePlay/TPS/ per ARCHITECTURE.md. Outputs a tight violations list — nothing else.
---

针对 `Assets/Scripts/GamePlay/TPS/` 的 diff 做边界 + 生命周期检查。规范见 `Assets/Scripts/GamePlay/TPS/ARCHITECTURE.md`。

# 流程

1. `git diff main...HEAD` 拿改动。用户传了 base 就用 base，否则默认 `main`。
2. 只看 `Assets/Scripts/GamePlay/TPS/` 下的改动行（含新增文件）。其他目录跳过。
3. 对每个改动文件按下面 8 条 grep + 读上下文。
4. 输出违规。没违规一行 `No issues found.`。

# 8 条检查

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
   例外（**OK 不报**）：组件作为某类 Actor 的"逻辑拥有者"（如 `WeaponComponent` 持 `List<Weapon>` / `Weapon currentWeapon`）—— 父持子裸引用是设计。判断："父是不是子的 Factory 创建者 + 生命周期管理者"，是 → 合法；否 → 真违规。
   修：真违规改成存 ID + `ActorWorld.Get<T>(id)` / `ViewManager.TryGetView(id)`。

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

8. **通用组件 cast Owner 为子类**
   类声明 `class XxxComponent : IActorComponent`（直接继承，不走 `ICharacterComponent` / `IBulletComponent` / `IWeaponComponent`），文件里出现 `Owner as Character` / `(Character)Owner` / `Owner as Bullet` / `Owner as Weapon` 等 cast——cast 即"我依赖子类字段"，违反"通用组件只读 Actor 基类字段"约定。
   例外（**OK 不报**）：
     - 子家族基类自己（`ICharacterComponent` / `IBulletComponent` / `IWeaponComponent`）的路由 cast——它们就是把通用 Actor 路由到强类型的中间层。
     - base 类是 `IActorComponent` 但**通过私有字段缓存 cast 后类型**（如 `private Bullet bullet = owner as Bullet;` 然后整文件用 `bullet.X`）— 这种本质是特化组件错放在 IActorComponent 上，**改回对应子家族**。
   修：要么改成对应 `IXxxComponent` 子家族（声明特化），要么把所需字段下沉到 Actor 基类。

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
