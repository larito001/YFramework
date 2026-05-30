# Animancer 武器动画指南（给美工）

**两个 .asset 分工**：
- **CharacterAnimSet**（角色级，每个角色一份）：**全部下半身 locomotion**——非瞄准 Idle/Walk/Run/Sprint（1D）+ 瞄准 Aim 8 方向 strafe（2D）+ Death + UpperBodyMask + 速度阈值
- **WeaponAnimSet**（武器级，每把枪一份）：**只含上半身**——IdleGunPose/AimPose 常驻持枪 pose + Shoot/Reload/Equip/Holster one-shot

切武器只换 WeaponAnimSet（上半身），下半身 locomotion / 瞄准 strafe mixer 保持连续不重建。每把枪只需要配自己的上半身 clip，不重复配 locomotion / aim / death。

> **近战不在这里**：近战、僵尸飞扑等"全身不可打断战斗动作"已上移为通用**技能系统**（`SkillDef` + `SkillCastComponent`），不再随武器配置。见 `Docs/技能系统使用指南.md` 与 `Docs/SkillDef 技能编辑指南.md`。

---

## 第一部分：CharacterAnimSet（角色动画包）

每个角色（Player / Enemy / NPC 等）建一份，含**全部下半身 locomotion**（非瞄准 + 瞄准 strafe）+ death + UpperBodyMask + 阈值。

### 创建

1. Project 窗口 → `Assets/Resources/Character/`（必须 Resources 下）
2. 右键 → Create → **TPS → CharacterAnimSet**
3. 命名 `<角色名>AnimSet.asset`（如 `PlayerAnimSet.asset`）

### 配字段

**非瞄准 Locomotion（LinearMixerState 1D，4 档）**
| 字段 | 拖什么 |
|---|---|
| Idle | 静止 idle |
| Walk | 慢走 |
| Run | 跑步 |
| Sprint | 冲刺 |

**瞄准下身 Locomotion（CartesianMixerState 2D，右键瞄准时下半身）** — 上半身被 WeaponAnimSet 的 AimPose 覆盖，这里只剩腿
| 字段 | 拖什么 |
|---|---|
| AimIdle | 瞄准静立（中心 (0,0)），也作 8 方向缺失时的兜底 |
| AimWalk | 1D 兜底：8 方向缺失时走任意方向用此 clip |
| AimWalkFwd / AimWalkBwd | 前 (0,1) / 后 (0,-1) |
| AimStrafeLeft / AimStrafeRight | 左 (-1,0) / 右 (1,0) |
| AimStrafeFL / FR / BL / BR | 4 对角线 |

**死亡 / 分层 / 阈值**
| 字段 | 拖什么 / 含义 |
|---|---|
| DeathL | 死亡变体 0（DeathVariant=0） |
| DeathR | 死亡变体 1（DeathVariant=1） |
| UpperBodyMask | 上半身 AvatarMask（让 WeaponAnimSet 的上身 Layer 1 只影响上半身，下半身走 Locomotion） |
| IdleThreshold | 默认 `0`（Idle 对应速度，一般 0） |
| WalkThreshold | 默认 `5`（一般 = `MoveComponent.WalkSpeed`） |
| RunThreshold | 默认 `6` |
| SprintThreshold | 默认 `7`（一般 = `MoveComponent.SprintSpeed`） |
| DefaultFade | locomotion 之间 / 触发 state 进入的淡入时长 |

> mixer 按 `AnimSpeedRatio`（角色当前真实 m/s）在相邻两档 clip 间平滑 blend，不是硬切。

### 告诉程序员路径

`Assets/Resources/Character/PlayerAnimSet.asset` → 告程序员 `Character/PlayerAnimSet`。

程序员在 CharacterFactory 给对应角色配 `character.CurrentCharacterAnimSetPath`。

---

## 第二部分：WeaponAnimSet（武器动画包）

每把枪建一份，**只配上半身相关**（不配 Locomotion / Death）。

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

选中刚建的 `.asset`，Inspector 里逐个拖。**WeaponAnimSet 只有上半身字段**——下半身 locomotion / 瞄准 8 方向 strafe / Death / UpperBodyMask / 速度阈值都在 **CharacterAnimSet**（见第一部分），这里不配。

**上身常驻 Pose（Layer 1 base，持武器时常驻；mask 取自 CharacterAnimSet.UpperBodyMask）**
| 字段 | 拖什么 |
|---|---|
| IdleGunPose | 站立持枪（未瞄准）的常驻 pose（上身循环）。持武器且未瞄准时 Layer 1 常驻这条 |
| AimPose | 瞄准持枪的常驻 pose（上身循环）。IsAiming=true 时切到这里。IdleGunPose / AimPose 互为兜底 |

**Combat 触发（上身 one-shot，叠在常驻 pose 上，播完回 base）**
| 字段 | 拖什么 |
|---|---|
| ShootLight | 单次开火（小后坐力，`HeavyRecoil=false` 走这条） |
| ShootHeavy | 单次开火（大后坐力，火箭筒类，`HeavyRecoil=true` 走这条） |
| Reload | 换弹 |
| Equip | 切到这把武器时播（拿出） |
| Holster | 切走这把武器时播（收回） |

**Fade 时长**
| 字段 | 默认 | 含义 |
|---|---|---|
| `ShootFade` | `0` | 开火触发的淡入时长。0=立即切让连发紧凑；0.05~0.1=轻微淡入平滑 |
| `AimPoseFade` | `0.15` | IdleGunPose ↔ AimPose 互切 / one-shot 播完回 base pose 的淡入时长（建议 0.12~0.2） |
| `UpperBodyEnterFade` | `0.15` | 装备瞬间（无武器→有）Layer 1 从 weight 0 升到常驻 base pose 的淡入（建议 0.1~0.2） |
| `UpperBodyExitFade` | `0.15` | 卸下武器（有→无）Layer 1 淡出到 weight 0 的时长（建议 0.15） |

> **未配任一持枪 pose（IdleGunPose / AimPose）** 时退化为旧式 Layer 1 one-shot（开火/换弹播完淡出整层）。要常驻持枪手感至少配一条 pose。

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
   - 鼠标右键 → 上身切 AimPose，下身走 AimIdle / 8 方向 strafe（瞄准 locomotion 在 CharacterAnimSet）
   - 鼠标左键 → ShootLight / Heavy（叠在上身 pose 上）
   - R 键 → Reload
   - 切别的武器 → Holster → Equip 链
   - V 键近战 → 走**技能系统**（SkillDef），不在本 .asset，见技能文档

---

## 常见错误

| 现象 | 原因 | 修法 |
|---|---|---|
| 切武器后角色 T-pose 不动 | 该武器没配 AnimSetPath（程序员侧）或 .asset 没拖任何 clip | 至少配 Idle clip 让默认有动画 |
| 控制台 `WeaponAnimSet 加载失败` | path 拼写错 / .asset 不在 Resources 下 | 检查程序员配的 path 跟资源实际路径 |
| 某个动作没切换 | 对应字段留空（null） | 拖 clip 进对应字段 |
| Shoot 动画播完前被打断 | 武器射速（FireInterval）很快 + clip 太长 | 让程序员调 `RecoilAnimSpeed`（武器字段）加速 clip 播放，或缩短 clip |
| Run / Sprint 不切 | CharacterAnimSet 的 `RunThreshold` / `SprintThreshold` 设错 | 调阈值（默认 6 / 7），或检查 Walk/Run/Sprint clip 是否拖了 |
| Aim 状态下走太快档位不对 | 瞄准下身是 8 方向 strafe 2D mixer（按 AnimMoveX/Y 选向，不分跑/冲档） | 这是当前设计；补全 8 方向 strafe clip 让各方向有动画 |
| 切武器有顿挫感 | CharacterAnimSet 的 `DefaultFade` 太小（硬切）或 clip 时长差距大 | 调大 DefaultFade（0.15s 平滑），或对齐 clip 长度 |
| 持枪上身没动 / 没常驻 pose | WeaponAnimSet 未配 IdleGunPose / AimPose | 至少配一条持枪 pose（互为兜底） |
| Reload 被走路打断 | （不该发生）一次性 state 自动锁定 | 如果发生反馈给程序，可能是 view 状态机 bug |

---

## 命名约定（建议）

| 资产类型 | 目录 | 命名 |
|---|---|---|
| WeaponAnimSet | `Assets/Resources/Weapon/Anim/` | `<武器名>.asset` |
| 武器专用 .anim clip | `Assets/Art/Weapon/<武器名>/Anim/` | `<动作>_<武器名>.anim`（如 `Shoot_Pistol.anim`） |

---

## 边界

- **分上下身已支持**：在 CharacterAnimSet 配 `UpperBodyMask` 后，WeaponAnimSet 的持枪 pose / Shoot / Reload 走 Layer 1（上半身），locomotion 走 Layer 0（全身）——可边跑边射、边走边换弹。不配 mask 则退化为单层（上身 one-shot 覆盖全身）
- **Locomotion 已平滑 blend**：非瞄准走 LinearMixerState（1D），瞄准走 CartesianMixerState（2D 8 方向），按角色真实 m/s（`AnimSpeedRatio`）在相邻 clip 间平滑过渡，不是硬切
- 弓箭 / 蓄力武器：当前没"蓄力" state 字段。需要加新字段 + 程序员加 view 状态机分支

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
