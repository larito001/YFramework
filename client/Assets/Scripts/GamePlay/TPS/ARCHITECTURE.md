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

### 目录组织

```
TPS/
├── Actor.cs / ActorWorld.cs / DamageInfo.cs / DamageRouter.cs / TimeScaleService.cs    顶级 service + 基础类
├── Components/                通用组件（IActorComponent 直接派生）+ 跨子系统耦合模块
│                              （HealthComponent / HitstopOnDamageComponent / GravityComponent /
│                               AutoDespawnComponent / FireEffect / SegmentRaycastMoveBase）
├── CharacterSystem/           Character Actor + 它的 Manager / Factory / View / 特化组件
├── WeaponSystem/              Weapon Actor + 它的 Manager / View / 特化组件
└── BulletSystem/              Bullet Actor + 它的 Manager / View / 特化组件
```

**约定**：
- 每个 Actor 子类对应一个 `XxxSystem/` 顶级子目录，下设 `Components/` + `Views/`
- 通用组件（任意 Actor 可挂）和**跨子系统耦合模块**（如 FireEffect 同时被 WeaponSystem 调用、产出 Bullet）放 `TPS/Components/`
- 如果某模块只服务单一 XxxSystem，就放进对应 `XxxSystem/Components/`；一旦发现第二个子系统也调它，挪到 `TPS/Components/`

**反例**（不该做）：`WeaponSystem/Components/FireEffect.cs`——FireEffect 同时依赖 Weapon 和 Bullet 两个子系统，归任一边都是"私有化耦合模块"。

---

## Actor

- ✅ 加字段（状态 / 意图 / trigger 分组注释）、组合组件、存 `OwnerXxxId`
- ❌ 写方法、持 GameObject / Transform / 别的 Actor 引用、加 getter setter

### 字段归属

**Actor 基类持有所有子类共用的字段**，子类只加自己专属字段。这是"通用组件"能跨 Actor 类型挂载的基础。

| 类 | 字段 |
|---|---|
| Actor | 空间（`Position` / `Rotation` / `WishVelocity` / `Velocity` / `IsGrounded`）、生命周期（`LifetimeRemaining` / `OwnerActorId`）、HP 系（`MaxHealth` / `CurHealth` / `IsDead` / `Die` / `DeathVariant`） |
| Character | 角色动画 / 武器持有 / 瞄准（`AnimMoveX/Y` / `AnimSpeedRatio` / 各 bool trigger / `AimTargetWorldPos` / `MuzzleHeight` / `CurrentWeaponSlot`） |
| Weapon | 开火 / 装弹 / 挂载（`FireOrigin` / `FireDirection` / `FireTarget` / `Mag*` / `CurrentAmmo` / `ReloadRequest` / `IsReloading` / `ShootEvent` / `Hand*` / `Back*`） |
| Bullet | `Damage` |

下沉到基类的字段允许某些子类用不到（Weapon HP 永远 0、Bullet 不写 WishVelocity）—— 几十字节内存换组件复用，是策略折中。

---

## Component

- ✅ 读写自家 Owner 字段、用 `Ctx?.TryGet` 拿 service、订阅 service 事件、Detach 清自己写过的字段
- ✅ **inline 调 service 的瞬时反馈 API**：相机抖屏、播音效、粒子触发、debug log——这些是动作伴随的单一紧耦合反馈
- ✅ **作为"子 Actor 的逻辑拥有者"持有子 Actor 列表 / 引用**：如 `WeaponComponent` 持 `List<Weapon> Weapons` + `Weapon currentWeapon`。父组件 Factory 构造子 Actor → `Manager.Adopt` 注册到 ActorWorld → 父 Detach 时 `Manager.Despawn` 清理。父子级"逻辑拥有"是设计模式，不算跨 actor 裸引用违规。
- ❌ 引用**平级** view / 别的 Actor / 别的 Component（非自己创建管理的子 Actor）、override `Attach(Actor)`、`GameObject.Find`
- ❌ **驱动多订阅者关心的状态变化派生效应**：damage→飘字/卡肉/清理这种"多个系统都要响应同一个状态变化"必须走事件订阅，不在逻辑组件里 inline 调 UI/Manager

判断准则（按这个就行，别纠结边界）：
- **预期只有 1 个反馈者** + **紧耦合于动作本身** → inline 调 service 即可（FireComponent → CameraShake、MeleeComponent → CameraShake）
- **多个系统订阅同一状态变化** → 逻辑组件只发事件（HealthComponent.OnDamaged 有 view 闪烁、飘字、卡肉、清理多个订阅者）

Tick 顺序由 Add 顺序决定，Factory 是唯一约定 Add 顺序的地方。有顺序依赖的组件**类注释里写明**。

### 挂载层级

- **通用组件**：直接继承 `IActorComponent`，Owner=Actor。**只读写 Actor 基类字段**。可挂任何 Actor 子类（Character / Weapon / Bullet / 未来的 NPC / Pickup / Destructible）。
  例：`HealthComponent` / `HitstopOnDamageComponent` / `GravityComponent` / `AutoDespawnComponent`。
- **特化组件**：继承 `ICharacterComponent` / `IWeaponComponent` / `IBulletComponent`，Owner=对应子类。需要子类专属字段时走这个。
  例：`MoveComponent` / `AimComponent` / `WeaponComponent` / `FireComponent` / `MeleeComponent` / `ReloadComponent` / `BulletMoveComponent` / `MissileMoveComponent`。

判断：组件 Tick 里**只读写 Actor 基类字段** → 通用组件；只要 cast Owner 取子类字段 → 特化组件。**通用组件里禁止 `Owner as Character` / `Owner as Bullet` 等 cast**——cast 即承认特化，应改回对应子家族继承。

通用组件需要"清理我"等 Manager 侧操作时，暴露 `event Action<Actor>` 让 Factory 装组件时由 Manager 订阅（如 `AutoDespawnComponent.OnDespawnReady`），不直接持 Manager 引用——这样组件依然对挂载 Manager 不可知。

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
| Actor → Actor（平级） | 存 ID，`ActorWorld.Get<T>(id)` 反查 |
| **父 Actor 持子 Actor 列表 / 引用**（如 WeaponComponent 持 List&lt;Weapon&gt;） | 父持子的裸引用 OK——父是子的"逻辑拥有者 + Factory 构造者 + 生命周期管理者"。Factory 构造后通过 `Manager.Adopt` 注册到 ActorWorld；父 Detach 时调 `Manager.Despawn` 清理子 |
| view → view | `ViewManager.TryGetView(id)` |
| 输入 / 业务事件 | service 暴露 `event Action`，订阅方 Attach 订阅 / Detach 退订 |
| **逻辑事件 → 状态衍生反馈**（damage→flytext/卡肉/清理） | 逻辑组件只 `Invoke` 事件，UI / 跨系统调用走 view 订阅或独立"订阅组件" |
| **动作伴随反馈**（fire→shake、swing→shake、播音效） | 单一紧耦合反馈可在逻辑组件里 inline 调 service（瞬时无状态 API） |
| 通用组件 → Manager（清理 / 通知） | 通用组件暴露 `event Action<Actor>`，Factory 装组件时让对应 Manager 订阅；组件不持 Manager 引用 |

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

## Weapon 持有者扩展

Weapon 体系不假设持有者类型——**Character / 塔 / 敌人 / 载具 / 任何 Actor 子类都能持武器**。
设计上拆成"通用"层 + "持有者专属"层：

### 通用层（持有者无关，已实现）

| 类 / API | 与持有者的关系 |
|---|---|
| `Weapon` Actor | 持有者只通过基类 `Actor.OwnerActorId`（int）记录 |
| `FireComponent` / `ReloadComponent` / `IWeaponComponent` | Owner=Weapon，完全不知持有者存在 |
| `FireEffect.Fire` | 签名参数化（origin/dir/target/ownerActorId），不接受 Weapon 类型 |
| `WeaponManager.Mount(Weapon, Actor, socket)` | owner 是 Actor 不是 Character |
| `WeaponView` | 通过 `viewMgr.TryGetView(OwnerActorId, out BaseView)` 找 socket，对持有者 view 类型无要求 |

### 持有者专属层（按 Actor 类型分写）

每种持有者写自己的"持枪人组件"，**不强求共用基类**——玩家、塔、敌人的"决策来源 / 朝向锁定 / 动画驱动" 差异太大。

| 持有者 | 持枪人组件 | 决策来源 | 动画驱动 |
|---|---|---|---|
| Character（玩家） | `WeaponComponent`（ICharacterComponent，已实现） | InputService 键盘 + 鼠标 | 写 Character 动画 trigger 字段 |
| Tower（塔） | `TowerWeaponHolderComponent`（未实现） | AI 目标锁定 + 射程判定 | 塔无动画或简单转炮塔 |
| Enemy（敌人） | `EnemyWeaponHolderComponent`（未实现） | AI 行为树 | 敌人动画 trigger（如有） |

### 持枪人组件协议

任何持枪人组件**必须做**：
1. 持有 `List<Weapon>`，Attach 时调 `weaponMgr.Adopt(weapon)` 移交注册
2. 维护"当前武器" 状态，切换时调 `weaponMgr.Mount/Unmount/MountOnBack` 改 mount socket
3. 每帧写 `currentWeapon.FireIntent / FireOrigin / FireDirection / FireTarget`——这是 FireComponent 消费的开火数据源
4. Detach 时调 `weaponMgr.Despawn(weapon)` 销毁子 Actor

**不该做**：
- 假设持有者类型是 Character（除非组件本身就是 Character 专属，如 `WeaponComponent` 监听 InputService）
- 直接调 Weapon 上的 FireComponent（FireComponent 自己 Tick，按 FireIntent 决定开火）
- 在持枪人组件里写 Weapon 内部状态（弹药 / cooldown / mount pose 都由 Weapon 子组件管）

---

## 三大不变量

1. **数据下游、状态上游**：组件写意图，view 读字段写 Unity 对象，view 物理后回写状态。view 不写意图。
2. **状态变化的派生反馈走事件订阅**：HP 扣血→飘字/卡肉/清理这种"多订阅者"场景必须发事件，不在逻辑组件里 inline 调 UI/Manager。**动作伴随反馈**（shake、音效、粒子）单一紧耦合的可以 inline。
3. **跨 actor 引用走 ID + ActorWorld**：不持裸引用。Service 默认不遍历 Actor，但允许通过 ActorWorld + id 受控写入特定 Actor 字段。

守住这三条，架构不会烂。
