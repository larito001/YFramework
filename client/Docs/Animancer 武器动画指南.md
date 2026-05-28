# Animancer 武器动画指南（给美工）

给每把武器配自己的全套动画——Idle / Walk / Run / Sprint / Aim* / Shoot / Reload / Equip / Holster / Melee / Die。
**用 Animancer 直接 Play AnimationClip，不用 Animator state machine 也不用 BlendTree**。
你只需要建一个 `WeaponAnimSet.asset` 资源 + 拖 clip 进去。

---

## 一句话原理

游戏运行时检测角色当前持有的武器 → 读 `WeaponAnimSet.asset`（你建的资源）→ 按角色当前状态（移动 / 开火 / 换弹 / 死亡 等）直接播对应 clip。**所有动画切换由代码控制，你只负责"哪个动作对应哪个 clip"**。

不需要拖 transition arrow，不需要建 BlendTree，不需要管 trigger 名。

---

## 准备工作

1. 玩家角色 prefab：`Assets/Resources/Player/Player.prefab`
   - 上面要有 `Animator` 组件（Animancer 内部用它），但 **Animator Controller 字段保持空**（运行时会被代码清空）
2. 为目标武器准备好各种 .anim 文件（如 `Idle_Pistol.anim` / `Shoot_Pistol.anim` / ...）
   - clip 文件可放 `Assets/Art/Weapon/<武器名>/Anim/` 或任意位置（不强制 Resources 下）

---

## 操作步骤

### 第 1 步：创建 WeaponAnimSet 资源

1. Project 窗口选 `Assets/Resources/Weapon/Anim/` 目录（**必须在 Resources 下**，程序才能 Load）
2. 右键 → Create → TPS → **WeaponAnimSet**
3. 命名规范：`<武器名>.asset`，例如 `Pistol.asset` / `Rifle.asset`

### 第 2 步：拖 clip 进字段

选中刚建的 `.asset`，Inspector 里逐个拖：

**Locomotion（无瞄准）** — 角色不持枪 / 持枪非瞄准时
| 字段 | 拖什么 | 示例 |
|---|---|---|
| Idle | 静止 idle | `Idle_Pistol.anim` |
| Walk | 慢走 | `Walk_Pistol.anim` |
| Run | 跑步 | `Run_Pistol.anim` |
| Sprint | 冲刺 | `Sprint_Pistol.anim` |

**Aim Locomotion（持枪瞄准时）** — 玩家右键按住时
| 字段 | 拖什么 |
|---|---|
| AimIdle | 持枪静立瞄准 |
| AimWalk | 持枪慢走瞄准 |

**Combat 触发** — 一次性动作
| 字段 | 拖什么 |
|---|---|
| ShootLight | 单次开火（小后坐力） |
| ShootHeavy | 单次开火（大后坐力，火箭筒类） |
| Reload | 换弹 |
| Equip | 取出武器到手中 |
| Holster | 收回武器 |

**近战变体** — 玩家按 V 触发
| 字段 | 触发条件 |
|---|---|
| MeleeHard | MeleeType=0（默认枪托砸） |
| MeleeKick | MeleeType=1（前踢） |

**死亡变体** — HP 归零触发
| 字段 | 触发条件 |
|---|---|
| DeathL | DeathVariant=0 |
| DeathR | DeathVariant=1 |

**Locomotion 阈值**（LinearMixerState 在相邻 clip 间平滑 blend，不是硬切）
- `IdleThreshold` 默认 0（Idle / AimIdle 对应的速度，一般 0）
- `WalkThreshold` 默认 1（Walk / AimWalk 对应的速度，建议等于 MoveComponent.WalkSpeed）
- `RunThreshold` 默认 1.6（Run 对应的速度）
- `SprintThreshold` 默认 2（Sprint 对应的速度）
- 数值含义：mixer 按 `AnimSpeedRatio`（角色当前速度）找最近两档 clip 平滑 blend——例如 Parameter=1.3 时 60% Walk + 40% Run blend

**分上下身（可选，AvatarMask）**
- `UpperBodyMask` 留空 → 单层模式（Combat 覆盖 Locomotion，开火 / 换弹时下半身停）
- 拖入上半身 AvatarMask → Combat 走 Layer 1（上半身），Locomotion 走 Layer 0（全身）—— 边跑边射 / 边走边换弹
- 创建上半身 mask：Project 右键 → Create → Avatar Mask → 选中 → Humanoid → 勾选上半身骨骼（Head/Body/Left Arm/Right Arm，取消 Root/Left Leg/Right Leg）

**Fade 时长**
- `DefaultFade` 0.1s：state 之间淡入时长，过小会硬切，过大会"软糖"
- `ShootFade` 0s：开火紧凑感，默认 0 立即切（按需调小 0.02-0.05 平滑）

### 第 3 步：不想配的字段留空

**可以**只配 Idle + Shoot + Reload，其他留空——view 检测到 clip null 会跳过对应 state（不闪、不报错）。
逐步补全更稳：先配最常见的 4 个动作（Idle / Walk / Shoot / Reload）看效果，再补其他。

### 第 4 步：告诉程序员路径

把 `.asset` 的 **Resources 相对路径**（去掉 `Assets/Resources/` 前缀、不带 `.asset` 后缀）告诉程序员：

| `.asset` 完整路径 | 告诉程序员的 path |
|---|---|
| `Assets/Resources/Weapon/Anim/Pistol.asset` | `Weapon/Anim/Pistol` |
| `Assets/Resources/Weapon/Anim/Bow.asset` | `Weapon/Anim/Bow` |

程序员把这个 path 配到对应武器的 `Weapon.AnimSetPath` 字段。

### 第 5 步：测试

1. 进游戏切到对应武器
2. 看角色是否按配置播 clip：
   - 站着不动 → Idle 循环
   - WASD 跑 → Walk → Run → Sprint 按速度切
   - 鼠标右键 → AimIdle / AimWalk
   - 鼠标左键 → ShootLight / Heavy
   - R 键 → Reload
   - V 键 → MeleeHard
   - 切别的武器 → Holster → Equip 链

---

## 常见错误

| 现象 | 原因 | 修法 |
|---|---|---|
| 切武器后角色 T-pose 不动 | 该武器没配 AnimSetPath（程序员侧）或 .asset 没拖任何 clip | 至少配 Idle clip 让默认有动画 |
| 控制台 `WeaponAnimSet 加载失败` | path 拼写错 / .asset 不在 Resources 下 | 检查程序员配的 path 跟资源实际路径 |
| 某个动作没切换 | 对应字段留空（null） | 拖 clip 进对应字段 |
| Shoot 动画播完前被打断 | 武器射速（FireInterval）很快 + clip 太长 | 让程序员调 `RecoilAnimSpeed`（武器字段）加速 clip 播放，或缩短 clip |
| Run / Sprint 不切 | `RunStartSpeed` / `SprintStartSpeed` 阈值设错 | 调小阈值，或者检查 Walk/Run/Sprint clip 是否拖了 |
| Aim 状态下角色继续播 Sprint | Aim 模式下只有 AimIdle / AimWalk 两档（无 AimRun / AimSprint） | 这是当前 MVP 设计，未来可加 Aim 时多档 |
| 切武器有顿挫感 | `DefaultFade` 太小（硬切）或 clip 时长差距大 | 调大 DefaultFade（0.15s 平滑），或对齐 clip 长度 |
| Reload 被走路打断 | （不该发生）一次性 state 自动锁定 | 如果发生反馈给程序，可能是 view 状态机 bug |

---

## 命名约定（建议）

| 资产类型 | 目录 | 命名 |
|---|---|---|
| WeaponAnimSet | `Assets/Resources/Weapon/Anim/` | `<武器名>.asset` |
| 武器专用 .anim clip | `Assets/Art/Weapon/<武器名>/Anim/` | `<动作>_<武器名>.anim`（如 `Shoot_Pistol.anim`） |

---

## 边界

- 当前**不分上下身**——Shoot 时会暂时覆盖 Locomotion（边跑边射的视觉效果不完美）。未来分 Animancer Layer + Avatar Mask 可让上半身 Shoot 跟下半身 Run 同时播
- 当前 Locomotion 是**阈值切 clip**（不是 BlendTree 平滑 blend）。Walk → Run 临界点会有微小切感。未来升级 LinearMixerState 平滑
- 弓箭 / 蓄力武器：当前没"蓄力" state 字段。需要加新字段+程序员加 view 状态机分支

---

## 谁负责什么

| 任务 | 负责人 |
|---|---|
| 武器专用 .anim clip 制作 | 美工 |
| `WeaponAnimSet.asset` 创建 + 拖 clip | 美工 |
| 速度阈值 / Fade 时长 调手感 | 美工（按预览效果） |
| `Weapon.AnimSetPath` 字段配置 | 程序员（在 CharacterFactory / TowerFactory 里） |
| 测试切武器是否正确 | 美工 + QA |
| 添加新 clip 类型（如蓄力）需要的 view 状态机改造 | 程序员 |
