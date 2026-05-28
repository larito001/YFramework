# TPS 架构原则

适用 `Assets/Scripts/GamePlay/TPS/`。冲突以本文为准。

---

## 分层

```
Service ── 跨对象单实例能力，被调端。默认不遍历 Actor；允许通过 ActorWorld + id 受控写入特定 Actor 字段
Manager ── 一类 Actor 的列表 + Tick 调度 + view 装载
Actor   ── 纯数据 + 组件容器
  └ Component ── 行为：Tick 读写自家 Owner 字段
View    ── Unity 渲染层：LateUpdate 读 Actor 写 Unity 对象
```

依赖方向：**Service ← Manager → Actor，Actor ← View（单向）**。组件不知道 view，view 不写意图。

---

## Actor

- ✅ 加字段（状态 / 意图 / trigger 分组注释）、组合组件、存 `OwnerXxxId`
- ❌ 写方法、持 GameObject / Transform / 别的 Actor 引用、加 getter setter

---

## Component

- ✅ 读写自家 Owner 字段、用 `Ctx?.TryGet` 拿 service、订阅 service 事件、Detach 清自己写过的字段
- ✅ **inline 调 service 的瞬时反馈 API**：相机抖屏、播音效、粒子触发、debug log——这些是动作伴随的单一紧耦合反馈
- ❌ 引用 view / 别的 Actor / 别的 Component、override `Attach(Actor)`、`GameObject.Find`
- ❌ **驱动多订阅者关心的状态变化派生效应**：damage→飘字/卡肉/清理这种"多个系统都要响应同一个状态变化"必须走事件订阅，不在逻辑组件里 inline 调 UI/Manager

判断准则（按这个就行，别纠结边界）：
- **预期只有 1 个反馈者** + **紧耦合于动作本身** → inline 调 service 即可（FireComponent → CameraShake、MeleeComponent → CameraShake）
- **多个系统订阅同一状态变化** → 逻辑组件只发事件（HealthComponent.OnDamaged 有 view 闪烁、飘字、卡肉、清理多个订阅者）

Tick 顺序由 Add 顺序决定，Factory 是唯一约定 Add 顺序的地方。有顺序依赖的组件**类注释里写明**。

---

## View

- ✅ LateUpdate 读 Actor 字段写 Unity 对象、回写状态字段（`Position` / `IsGrounded`）、消费 trigger 清回、订阅自家 Actor 事件、`ViewManager.TryGetView` 找别的 actor 的 view
- ❌ **写意图字段**、做游戏决策、被 Actor 持有、持别的 view 引用、`GameObject.Find`

---

## Manager

- ✅ `Init` 拿依赖、维护 `List<TActor>` + Tick 遍历、Spawn/Despawn 流程、暴露 deferred 接口让组件在 Tick 中安全调
- ❌ Tick 里现拿 service、立刻删 list 元素（用 toRemove 暂存）、持别的 Manager 的 Actor 引用

---

## Service

- ✅ 暴露 API + event 被调，持有自身业务状态
- ✅ 通过 `ActorWorld.Get<T>(id)` 对**调用方指定的特定 Actor** 做受控写入（如 `TimeScaleService` 拿到 id 后写 `actor.TimeScale`）
- ❌ 主动遍历 Actor 列表干活、按业务逻辑筛选 Actor 操作（纯查询如 `ActorWorld` 例外）

---

## 跨对象通信

| 场景 | 通道 |
|---|---|
| Actor → Actor | 存 ID，`ActorWorld.Get<T>(id)` 反查 |
| view → view | `ViewManager.TryGetView(id)` |
| 输入 / 业务事件 | service 暴露 `event Action`，订阅方 Attach 订阅 / Detach 退订 |
| **逻辑事件 → 状态衍生反馈**（damage→flytext/卡肉/清理） | 逻辑组件只 `Invoke` 事件，UI / 跨系统调用走 view 订阅或独立"订阅组件" |
| **动作伴随反馈**（fire→shake、swing→shake、播音效） | 单一紧耦合反馈可在逻辑组件里 inline 调 service（瞬时无状态 API） |

禁：组件持 view 引用、view 持别的 view 引用、Actor 之间 `==` 引用、`GameObject.Find` 系列。

---

## Tick 顺序

实际注册顺序见 `GameBootstrapper.BuildContext`。**有数据依赖、不能乱序的只有**：

```
TimeScaleService → CharacterManager → WeaponManager → BulletManager
```

依赖前提：
- TimeScaleService 必须在 Actor Manager 之前——它的 Tick 用 unscaledDt 写 `actor.TimeScale` / `Time.timeScale`，同帧后续 Manager Tick 才能读到当帧的缩放值
- WeaponManager 在 CharacterManager 之后——武器开火参数依赖 Character 当帧写完的 `FireOrigin / FireDirection`
- BulletManager 在 WeaponManager 之后——子弹 spawn 依赖 Weapon 当帧写完的字段

其他 service（InputService、CameraManager、FlyTextMgr、网络层等）Tick 顺序无关紧要——它们之间无数据依赖。CameraManager 等 framework service 实际在 GameBootstrapper 早期就注册了，远在 Actor Manager 之前。

---

## 三大不变量

1. **数据下游、状态上游**：组件写意图，view 读字段写 Unity 对象，view 物理后回写状态。view 不写意图。
2. **状态变化的派生反馈走事件订阅**：HP 扣血→飘字/卡肉/清理这种"多订阅者"场景必须发事件，不在逻辑组件里 inline 调 UI/Manager。**动作伴随反馈**（shake、音效、粒子）单一紧耦合的可以 inline。
3. **跨 actor 引用走 ID + ActorWorld**：不持裸引用。Service 默认不遍历 Actor，但允许通过 ActorWorld + id 受控写入特定 Actor 字段。

守住这三条，架构不会烂。
