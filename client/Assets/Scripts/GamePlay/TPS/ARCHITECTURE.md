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
├── TowerSystem/               Tower Actor + 它的 Manager / Factory / View + TowerWeaponComponent
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
| Actor | 空间（`Position` / `Rotation` / `WishVelocity` / `Velocity` / `IsGrounded`）、生命周期（`LifetimeRemaining` / `OwnerActorId`）、阵营（`TeamId`）、HP 系（`MaxHealth` / `CurHealth` / `IsDead` / `Die` / `DeathVariant`） |
| Character | 角色动画 / 武器持有 / 瞄准（`AnimMoveX/Y` / `AnimSpeedRatio` / 各 bool trigger / `AimTargetWorldPos` / `MuzzleHeight` / `CurrentWeaponSlot` / `CurrentWeaponAnimSetPath` + `WeaponAnimDirty`） |
| Weapon | 开火几何（`MuzzleLocalOffset` 武器自配的枪口偏移）/ 开火意图（`FireOrigin` / `FireDirection` / `FireTarget` 持枪人每帧写）/ 装弹（`Mag*` / `CurrentAmmo` / `ReloadRequest` / `IsReloading`）/ 挂载（`Hand*` / `Back*` / `ShootEvent`）/ 动画（`AnimSetPath` 武器自带的 WeaponAnimSet ScriptableObject 资源路径） |
| Tower | `TargetActorId`（TowerTargetingComponent 写、TowerWeaponComponent 读，实现 targeting / 持枪人 解耦） |
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
- **特化组件**：继承 `ICharacterComponent` / `IWeaponComponent` / `IBulletComponent` / `ITowerComponent`，Owner=对应子类。需要子类专属字段时走这个，或语义上只该挂某子类时也走这个（即便当前没专属字段）。
  例：`MoveComponent` / `AimComponent` / `WeaponComponent` / `FireComponent` / `MeleeComponent` / `ReloadComponent` / `BulletMoveComponent` / `MissileMoveComponent` / `TowerTargetingComponent` / `TowerWeaponComponent`。

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
TimeScaleService → CharacterManager → TowerManager → WeaponManager → BulletManager
```

依赖前提：
- TimeScaleService 必须在 Actor Manager 之前——它的 Tick 用 unscaledDt 写 `actor.TimeScale` / `Time.timeScale`，同帧后续 Manager Tick 才能读到当帧的缩放值
- WeaponManager 在 CharacterManager / TowerManager 之后——武器开火参数依赖持枪人组件当帧写完的 `FireOrigin / FireDirection / FireIntent`（玩家走 WeaponComponent，塔走 TowerWeaponComponent）
- BulletManager 在 WeaponManager 之后——子弹 spawn 依赖 Weapon 当帧写完的字段

其他 service（InputService、CameraManager、FlyTextMgr、网络层等）Tick 顺序无关紧要——它们之间无数据依赖。CameraManager 等 framework service 实际在 GameBootstrapper 早期就注册了，远在 Actor Manager 之前。

---

## 阵营

`Actor.TeamId int` 字段标识阵营归属：

| TeamId | 含义 |
|---|---|
| 0 | 中立。子弹 / 环境 / 未配置的 Actor 默认值 |
| 1 | 玩家军（Character 玩家、玩家方塔） |
| 2 | 敌军（Dummy、敌方塔、未来的 AI 敌人） |
| 3+ | 扩展（NPC 派系 / 多人 PvP 队伍）|

**约定**：
- **友军伤害默认禁用**：`HealthComponent.ApplyDamage` 检查 `info.AttackerTeamId == Owner.TeamId && Owner.TeamId != 0` 时跳过扣血。两边任一为 0（中立）正常扣血——中立既能伤别人也能被伤。
- **Weapon.TeamId 跟随持有者**：`WeaponManager.Mount/MountOnBack` 时写 `weapon.TeamId = owner.TeamId`。换持有者自动更新。
- **Bullet.TeamId 跟随发射者**：`BulletManager.SpawnBullet` 时由 `FireEffect.Fire` 传入。子弹命中扣血时把 `Owner.TeamId` 打进 `DamageInfo.AttackerTeamId`。
- **DamageRouter.RaycastSkipActor 不过滤友军**：友军挡子弹（视觉上"打中了"），但 HealthComponent 拦扣血。未来要"友军不挡子弹"再加 teamMask 参数。
- **塔 AI 锁敌**：扫 `ActorWorld.GetAll` + 按 `target.TeamId != self.TeamId && target.TeamId != 0` 筛。

---

## 伤害系统

伤害链路 + 公式 + 扩展点。所有"打了谁、扣多少血、附带什么效果" 都走这条链。

### DamageSpec vs DamageInfo

| 类型 | 角色 | 谁持有 | 什么时候构建 |
|---|---|---|---|
| `DamageSpec` | **配置模板**：基础伤害 / 卡肉 / 暴击概率 / 暴击倍率 / 元素 / 携带 buff | 攻击者侧组件（MeleeComponent.Damage / FireComponent.Damage / Bullet.Damage） | Factory 配（object initializer） |
| `DamageInfo` | **运行时实例**：spec + 上下文（AttackerId / TeamId）+ roll 结果（IsCritical/CritMultiplier）+ 输出（FinalAmount） | 临时 struct，不持有 | 命中那一帧 `DamageInfo.Build(in spec, attackerId, teamId)` |

**好处**：攻击者组件只配一份 spec，命中代码永远一行 `var info = DamageInfo.Build(in Damage, attackerId, teamId);`。加暴击/元素/buff 只改 spec 字段值，命中代码不变。

### 链路

```
攻击者侧                           DamageRouter                      目标侧
─────────                          ─────────────                     ─────────
DamageInfo.Build(in spec, ...)  →  ApplyToActor / TryHit          →   HealthComponent.ApplyDamage
   ↑ Build 内部 roll 暴击            ↑ collider→ActorID→HealthComp     ↓
   spec.Element / AppliedBuffs                                       DamageCalculator.ComputeFinalDamage
   原样透传到 info                                                     ↓
                                                                     扣 HP / Invoke OnDamaged(含 FinalAmount)
                                                                     ↓
                                                                     AppliedBuffs → target.BuffComponent.AddBuff
```

### DamageInfo 字段

| 字段 | 谁填 | 用途 |
|---|---|---|
| `Amount` | 攻击者 | 基础伤害（未放缩） |
| `AttackerId` | 攻击者 | actor.ID，自伤过滤 + 击杀归属 |
| `AttackerTeamId` | 攻击者 | 友军伤害过滤 |
| `HitstopTier` | 攻击者 | 命中卡肉分级 |
| `IsCritical` | 攻击者（roll） | 暴击标记 |
| `CritMultiplier` | 攻击者 | 暴击倍率（默认 1.0 = 不放大） |
| `Element` | 攻击者（武器/技能） | 元素属性，目标方查抗性 |
| `AppliedBuffs` | 攻击者（武器/技能） | 命中后转给目标 BuffComponent 应用 |
| `FinalAmount` | **DamageCalculator + HealthComponent 写回** | 实际扣的 HP（view 飘字读这个） |

### 责任划分

| 模块 | 职责 |
|---|---|
| **攻击者侧组件**（FireComponent / MeleeComponent / 技能 / AI） | 持 `DamageSpec` 字段（Factory 配）；命中时一行 `DamageInfo.Build(in spec, attackerId, teamId)` |
| **DamageInfo.Build**（static factory） | 从 spec 组装 DamageInfo；内部 roll 暴击（Random.value &lt; spec.CritChance）；spec 其他字段（Element / Buffs）原样透传 |
| **DamageRouter** | 路由 Collider → Actor.ID → HealthComponent，过滤自伤 |
| **DamageCalculator**（static） | 公式集中处：暴击放大 × 元素抗性 × 减伤 buff 全部走这。无副作用、无 HP 写入 |
| **HealthComponent** | 阵营过滤 → 调 calculator → 扣 HP → 回填 FinalAmount → Invoke 事件 → 调 BuffComponent.AddBuff → 死亡链路 |
| **target.BuffComponent**（未来完整化） | 接收 AppliedBuffs，管 buff timer / stack / 减伤 / DOT / 属性修改 |
| **target.ResistComponent**（未实现） | 按 Element 提供抗性值给 calculator |

### 公式协议

所有放大 / 折扣 / 减免按**乘法叠加**：

```
final = base * crit * (1 - resist) * (1 - mitigation) * ...
```

加法易堆出 0 / 负值；乘法每项独立可调，策划友好。`final < 0` 会被 Mathf.Max(0) 夹住——未来要"治疗 = 负伤害" 走 HealthComponent.Heal 独立路径，不混入伤害链。

### 扩展点（架构留口，未来扩展按此走，**不破坏现有契约 + 不改命中代码**）

加任意"扩展" 都只改 DamageSpec 配置值或 calculator 公式，攻击者命中代码（DamageInfo.Build 那一行）永远不变。

- **暴击**：spec 已有 CritChance / CritMultiplier。Factory 配 `Damage = new DamageSpec { BaseDamage=25, CritChance=0.1f, CritMultiplier=1.5f }`，命中自动 roll
- **元素**：spec 已有 Element。Factory 配 `Element = DamageElement.Fire`。未来加 `ResistComponent : IActorComponent`（暴露 GetResist），calculator 取消 TODO 注释接入
- **buff 应用**：spec 已有 AppliedBuffs。Factory 配 `AppliedBuffs = new List<BuffSpec> { new BuffSpec(BUFF_BURN, 5f) }`。BuffComponent.AddBuff 已就位（占位实现），完整化时内部 List + Tick + timer
- **减伤 buff**：BuffComponent 加 `GetDamageMitigation(DamageElement)`，calculator 取消 TODO 注释接入
- **攻击者属性加成**（攻击力%/暴击率加成等）：新建 `CombatStatsComponent`，在 `DamageInfo.Build` 里读 attacker 的 stats 修改 spec.BaseDamage / spec.CritChance（或专门写 BuildFromStats helper）

### 禁止

- ❌ 攻击者侧组件直接调 `target.HealthComponent.ApplyDamage`——必须走 DamageRouter，过滤自伤 / 阵营才能集中
- ❌ HealthComponent 内部加复杂公式——公式都进 DamageCalculator，HealthComponent 只做"HP 数学 + 事件 + 副作用调度"
- ❌ 任何模块直接读写 `Owner.CurHealth`——所有扣血走 ApplyDamage，所有加血走 Heal
- ❌ 把"应用 buff" 散到攻击者侧——攻击者只填 AppliedBuffs，应用在目标侧 HealthComponent 统一调

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
| Tower（塔） | `TowerWeaponComponent` + `TowerTargetingComponent`（ITowerComponent，已实现） | TowerTargetingComponent 扫 ActorWorld 找最近敌人写 Tower.TargetActorId；TowerWeaponComponent 读 TargetActorId + AimTime telegraph | 塔无动画，TowerTargetingComponent 旋转 Owner.Rotation 朝目标 |
| Enemy（敌人） | `EnemyWeaponHolderComponent`（未实现） | AI 行为树 | 敌人动画 trigger（如有） |

### 持枪人组件协议

任何持枪人组件**必须做**：
1. 持有 `List<Weapon>`，Attach 时调 `weaponMgr.Adopt(weapon)` 移交注册
2. 维护"当前武器" 状态，切换时调 `weaponMgr.Mount/Unmount/MountOnBack` 改 mount socket
3. 每帧写 `currentWeapon.FireIntent / FireOrigin / FireDirection / FireTarget`——这是 FireComponent 消费的开火数据源。`FireOrigin` 由武器侧配的 `MuzzleLocalOffset` 决定具体偏移：`FireOrigin = holder.Position + holder.Rotation * currentWeapon.MuzzleLocalOffset`，持枪人组件只机械应用，不该硬编码 forward / height 数值
4. 切枪 / Detach 时把 `currentWeapon.AnimSetPath` 镜像写到 `holder.CurrentWeaponAnimSetPath` + 置 `holder.WeaponAnimDirty=true`，由 view 加载对应 WeaponAnimSet ScriptableObject 切换动画（详见"动画接口协议"小节）
4. Detach 时调 `weaponMgr.Despawn(weapon)` 销毁子 Actor

**不该做**：
- 假设持有者类型是 Character（除非组件本身就是 Character 专属，如 `WeaponComponent` 监听 InputService）
- 直接调 Weapon 上的 FireComponent（FireComponent 自己 Tick，按 FireIntent 决定开火）
- 在持枪人组件里写 Weapon 内部状态（弹药 / cooldown / mount pose 都由 Weapon 子组件管）

---

## 动画接口协议

Character 上的动画字段是**"逻辑组件 → view" 协议层**：

```
WeaponComponent / MeleeComponent / HealthComponent 等逻辑组件
                  ↓ 写
Character.{Shoot/Reload/WeaponSwap/WeaponHolster/MeleeAttack/MeleeType/Die/DeathVariant/
           IsAiming/IsShooting/IsReloading/HeavyRecoil/RecoilAnimSpeed/
           AnimMoveX/Y/AnimSpeedRatio/AnimPlaybackRate/
           CurrentWeaponAnimSetPath/WeaponAnimDirty}
                  ↓ 读
CharacterView.LateUpdate
                  ↓ 调用任何方案
Unity Animator / Animancer / 自研 Playables
```

**view 内部可以用任何动画方案实现**——只要消费 Character 协议字段即可。**当前实现：Animancer + WeaponAnimSet ScriptableObject + LinearMixerState + Layer/AvatarMask 分上下身**：
- Weapon 上配 `AnimSetPath`（指向 Resources 下的 .asset）
- 武器切换时 view 加载 `WeaponAnimSet`（含 Idle/Walk/Run/Sprint/Aim*/Shoot/Reload/Equip/Holster/Melee/Die 各 AnimationClip + 阈值 + UpperBodyMask）
- **Locomotion 用 `LinearMixerState` 平滑 blend**：4 child (Idle/Walk/Run/Sprint) 按 `IdleThreshold/WalkThreshold/RunThreshold/SprintThreshold` 平滑过渡；Aim 模式切到 aimLocomotionMixer（2 child）
- **Combat 走 Layer 1 + AvatarMask 上下身分离**：UpperBodyMask 配了时上半身播 Shoot/Reload/Equip/Holster/Melee，下半身继续走 Locomotion mixer（边跑边射）；mask=null 时单层模式，Combat 覆盖 Locomotion
- **状态优先级**：Die（Layer 0 全身覆盖 + Layer 1 fade out）&gt; Melee &gt; Holster &gt; Equip &gt; Reload &gt; Shoot
- 一次性 state（双层模式）播完自动 fade out Layer 1 让 Locomotion 透回；单层模式走 IsPlaying 跟踪

**架构层选择权**：换回 Unity Animator / 升级 Animancer LinearMixerState / 自研都不破坏 ARCHITECTURE——只重写 `CharacterView.LateUpdate` + `DriveAnimation`，逻辑组件零改动。

### 给美工

加新武器动画的操作步骤详见 `docs/Animancer 武器动画指南.md`。Factory 配 `Weapon.AnimSetPath = "Resources 相对路径"` 程序员负责，`WeaponAnimSet.asset` 美工创建 + 拖 clip。

---

## 三大不变量

1. **数据下游、状态上游**：组件写意图，view 读字段写 Unity 对象，view 物理后回写状态。view 不写意图。
2. **状态变化的派生反馈走事件订阅**：HP 扣血→飘字/卡肉/清理这种"多订阅者"场景必须发事件，不在逻辑组件里 inline 调 UI/Manager。**动作伴随反馈**（shake、音效、粒子）单一紧耦合的可以 inline。
3. **跨 actor 引用走 ID + ActorWorld**：不持裸引用。Service 默认不遍历 Actor，但允许通过 ActorWorld + id 受控写入特定 Actor 字段。

守住这三条，架构不会烂。
