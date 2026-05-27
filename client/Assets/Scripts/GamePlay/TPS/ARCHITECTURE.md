# TPS 架构规范

适用范围：`Assets/Scripts/GamePlay/TPS/` 下所有代码（Character / Weapon / Bullet 及后续新增的 Actor 类型）。

新加文件、改老代码、Code Review 都按这份对照。冲突时以本文为准；要破例先把例外原因加注释。

---

## 1. 三层模型

每个游戏对象拆成三层，互不越权：

| 层 | 内容 | 不可做 |
|---|---|---|
| **Actor** | 纯数据字段 + ID + 组件容器 | 不写行为方法、不引用 GameObject / Component / Transform |
| **Component**（`IxxxComponent`） | Tick 里读写 Owner 字段实现行为 | 不引用 view、不调 `Find/GetComponent` |
| **View**（`xxxView : BaseView`） | `LateUpdate` 消费 Actor 字段驱动 Unity 渲染 | 不写游戏逻辑、不被 Actor 持有 |

加一个 **Manager**（`IGameService` + 通常 `ITickable`）做这一类 Actor 的列表、Tick 调度、view 装载/卸载。

---

## 2. 目录与命名

```
TPS/
  Actor.cs                      ← 基类 + IActorComponent 契约
  ActorWorld.cs                 ← 全局 ID→Actor 注册表
  <System>System/
    <Type>.cs                   ← Actor 子类（纯数据）
    <Type>Manager.cs            ← IGameService + ITickable
    <Type>Factory.cs            ← 可选：装组件 + 加载 view
    Components/
      I<Type>Component.cs       ← 强类型组件基类
      <Name>Component.cs        ← 具体组件
    Views/
      <Type>View.cs             ← 继承 BaseView
```

命名硬规则：
- 组件基类前缀 `I`（`ICharacterComponent` / `IWeaponComponent` / `IBulletComponent`），尽管是 class 不是 interface —— 保持已有风格，全项目统一。
- 具体组件后缀 `Component`（`AimComponent`、`FireComponent`）。
- view 后缀 `View`，挂在场景物体上的 MonoBehaviour。
- Manager 后缀 `Manager`，service 后缀 `Service`。

---

## 3. Actor 规则

- 继承 `Actor`，**只加字段**，不写方法（除非是 Unity 不可避免的 Dispose override）。
- 字段必须按用途分组写注释：
  - **状态字段**：view 物理后回写、组件读取（如 `Character.Position` / `IsGrounded`）。
  - **意图字段**：组件写、view 读取应用（如 `WishVelocity` / `Rotation` / `AnimMoveX`）。
  - **一次性 trigger 字段**：组件置 `true`、view 消费后清回 `false`（如 `MeleeAttack` / `WeaponSwap`）。
- 字段默认 `public`，访问者自律。不要为了"封装"加属性 getter/setter，徒增噪音。
- `ID` 不要自己赋值，基类构造时自增分配。
- 跨 Actor **绝不持有对方引用**，只存 `int OwnerXxxId`，用时去 `ActorWorld.Get<T>(id)` 查。

---

## 4. Component 规则

### 4.1 基本

- 继承对应的 `I<Type>Component` 基类，override **强类型** `Attach(Owner)` / `Tick(dt)` / `Detach()`。**不要** override `Attach(Actor)`（已被基类 sealed）。
- 依赖服务通过基类提供的 `Ctx` 拿：`Ctx?.TryGet(out service)`。**不要**自己写 `GameLoop.Instance.Ctx` 链 —— `IActorComponent.Attach` 已经统一拉好，Detach 时也会自动清。
- `Tick` 第一行做空保护：`if (input == null || Owner == null) return;`。
- **只读写 Owner 字段**。要影响别的 Actor，写自己 Owner 的字段让对方组件下一帧读，或通过 service 接口（如 `BulletManager.Spawn`）。
- 组件之间**不互相引用**。要共享中间结果就经 Owner 字段中转。

### 4.2 Tick 顺序与依赖

- 组件 Add 顺序 = Tick 顺序。Factory 是唯一约定 Add 顺序的地方。
- 运行时插组件用 `Owner.AddBefore<TAnchor>(comp)` / `AddAfter<TAnchor>(comp)`，不要用裸 `Add` —— 那会强制 append，可能违反顺序约束。
- 新增组件如果对前后顺序有要求，**必须在类注释里写明** "必须在 X 之前/之后"，并说明原因（参考 `AimComponent` / `WeaponComponent` 的注释模板）。

### 4.3 动态增减契约（重要）

- `Owner.Remove<T>()` / `Owner.Remove(comp)` 可在任意时刻调用。Tick 期间调用会延迟到 Tick 末执行，**不会**导致迭代失稳。
- **Detach 必须把自己写过的 Owner 字段清回默认值**。这是动态移除场景下避免 reader 读到死值的关键。模板：

  ```csharp
  public override void Detach()
  {
      if (Owner != null)
      {
          Owner.MyFieldA = default;
          Owner.MyFieldB = ...;
      }
      cachedService = null;     // 清服务引用
      // 退订事件：input.OnXxx -= Handler;
      base.Detach();            // 末尾调 base，base 会清 Owner 和 Ctx
  }
  ```

- **`base.Detach()` 必须在末尾调用**，因为 base 会把 `Owner = null`、`Ctx = null`，提前调就用不到 Owner 了。
- 同类型多实例（Buff 叠加等）用 `Owner.GetAll<T>(buffer)` 写入外部 list 遍历，避免 enumerator GC。`Get<T>()` 只返回第一个。

---

## 5. View 规则

- 继承 `BaseView`，实现 `Bind(Actor actor, int id)` 拿到强类型 Actor 引用。
- 驱动 Unity 对象的代码放 **`LateUpdate`**，保证当帧所有组件 Tick 已经写完意图字段。
- view 是被动的：只读 Actor 字段、回写少量物理状态（`Position` / `IsGrounded`），不做决策。
- 一次性 trigger 字段消费后立刻清回（`character.MeleeAttack = false`）。
- 跨 view 引用（如 `WeaponView` 找角色 socket）走 `ViewManager.TryGetView(id)`，不要 `GameObject.Find`。
- 池化 view 实现 `IPoolable`，`OnDespawn` 必须清掉所有 Actor 引用字段，防止下次 Get 出来还指向旧对象。

---

## 6. Manager 规则

- 实现 `IGameService`（`Init` / `Shutdown`）。需要每帧驱动的实现 `ITickable`。
- `Init` 里从 `GameContext` 拿 `ViewManager` / `ActorWorld` 等服务，**别在 `Tick` 里现拿**。
- Spawn 流程固定为：
  1. `new` Actor + 设字段 + `Add` 组件
  2. 加入自己的 `List<T>`
  3. `world.Register(actor)`
  4. 加载 view：`viewMgr.LoadBaseView<TView>(path, actor)` 或 `pool.Get(...)` + `view.Bind`
- Despawn 流程反序：`world.Unregister(id)` → 释放 view → `actor.Dispose()` → 从 list 移除。
- **遍历中要删元素**：先 push 到 `toRemove` 暂存，`Tick` 末尾批量 `RemoveImmediate`。参考 `BulletManager.Tick`。
- `Shutdown` 倒序清空所有 Actor，释放 pool 和程序化 prefab 原型。

---

## 7. 跨对象通信

| 场景 | 怎么做 |
|---|---|
| 同 Actor 内组件互通 | 读写 Owner 字段 |
| Actor → 自家 view | view 持 Actor 引用，单向读 |
| view 回写物理结果 | 写 Actor 的"状态字段"（注释标 view 写、组件读） |
| Actor A → Actor B 数据 | A 写 B 的 ID 字段 → 别人 `ActorWorld.Get<TB>(id)` 查 B |
| view → 别的 view | `ViewManager.TryGetView(id)` |
| 一次性事件（输入按下） | `InputService` 上的 `event Action`，组件 `Attach` 订阅、`Detach` 退订 |
| 高频生成（子弹、特效） | 调对应 Manager 的 `Spawn(...)` 接口 |

禁止：组件持有 view 引用、view 持有别的 view 引用、Actor 之间直接 `==` 引用、`GameObject.Find` 系列、`FindObjectOfType`。

---

## 8. 池化使用

什么时候池化：
- **要池**：高频生成、短命的 view（子弹、弹壳、特效、伤害飞字）。判据：单局生成次数 > 50 或 间隔 < 1s。
- **不池**：单实例或长命的 view（角色、武器模型）。直接 `ViewManager.LoadBaseView`。

池化 view 的实现要点（对照 `BulletView` + `BulletManager.Init`）：

1. view 实现 `IPoolable`。`OnDespawn` 把所有 Actor 引用字段清成 `null` / `-1`。
2. pool 的 prefab 原型若程序化构造（`CreatePrimitive` 等），**删 Collider 用 `DestroyImmediate`**，不能用 `Destroy`。Destroy 排队到帧末，prewarm 期间 Instantiate 出来的克隆会继承 Collider，导致子弹互射、撞自己等怪 bug。
3. pool 的 view **不进 `ViewManager`**，pool 自管生命周期，无人按 ID 反查。
4. `Manager.Shutdown` 里 `pool.Dispose()` + 销毁 prototype GO。

---

## 9. Tick 顺序

GameLoop 调 Manager 的顺序就是逻辑分层：

```
CharacterManager  →  WeaponManager  →  BulletManager
```

原因：武器开火参数依赖 Character 当帧写完的 `Position` / `Rotation` / `AimTargetWorldPos`；子弹 spawn 依赖 Weapon 当帧写完的 `FireOrigin` / `FireDirection`。

Actor 内组件 Tick 顺序由 `Add` 决定，**`Factory` 是唯一约定 Add 顺序的地方**。其他人不要在运行时插组件。

---

## 10. 新增 Actor 类型 Checklist

1. `<Type>.cs` —— 继承 `Actor`，加字段，按"状态/意图/trigger"分组写注释。
2. `Components/I<Type>Component.cs` —— 强类型组件基类，照 `ICharacterComponent` 抄一份。
3. `Components/<Name>Component.cs` —— 具体组件。
4. `Views/<Type>View.cs` —— 继承 `BaseView`，`Bind` + `LateUpdate`。高频生成的加 `IPoolable`。
5. `<Type>Manager.cs` —— `IGameService` + `ITickable`，Spawn/Despawn/Tick。
6. （可选）`<Type>Factory.cs` —— 装组件 + 加载 view 的配方代码。
7. 在 GameLoop 启动流程里注册 Manager，确认 Tick 顺序在依赖之后。
8. 跨类型引用走 ID + `ActorWorld`，绝不持裸引用。
