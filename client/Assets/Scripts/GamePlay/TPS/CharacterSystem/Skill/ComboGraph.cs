using System;
using UnityEngine;

/// <summary>
/// **连招招式图**（数据资产）：把若干"招式"（<see cref="SkillDef"/>）编排成一张有向图——
/// 节点 = 一个招式，边 = "在某招式的取消窗内、按某个键（+可选方向）→ 跳到下一招"。
/// 由 <see cref="ComboComponent"/> 驱动；每招的执行 / 位移 / 命中 / 吸附仍由 <see cref="SkillCastComponent"/> 负责。
///
/// **约定**：Nodes[0] 是 **Neutral 入口节点**（Skill 留空），它的 Links 表示"从站立起手"——
/// 按 Light / Heavy 等键各起一招。其余节点的 Links = 该招式的后续派生（连招分支）。
///
/// **连招时机**：默认沿用目标段落到的 SkillCast 取消窗（<see cref="SkillDef.SkillSegment.CancelFromNorm"/>），
/// 无需在边上重配——招式的取消窗开了，连招才接得上；没开就照常播完（含完整后摇）。
///
/// **编辑流程**（纯 Inspector）：Create → TPS → ComboGraph，配 Nodes，把资产 Resources 相对路径填到武器
/// <see cref="Weapon.ComboGraphPath"/>。范例（三段轻击 + 重击分支）：
///   [0] Neutral  Skill=∅   Links={ (Light→1), (Heavy→4) }
///   [1] Atk1     Skill=Knife_Atk1   Links={ (Light→2) }
///   [2] Atk2     Skill=Knife_Atk2   Links={ (Light→3), (Heavy→4) }   // 轻击续段 / 重击转收尾
///   [3] Atk3     Skill=Knife_Atk3   Links={}                          // 末段，无后续=连段结束
///   [4] Heavy    Skill=Knife_Heavy  Links={}
/// </summary>
[CreateAssetMenu(fileName = "ComboGraph", menuName = "TPS/ComboGraph", order = 103)]
public class ComboGraph : ScriptableObject
{
    [Header("手感 / 时机（本连招专属，策划调）")]
    [Tooltip("输入缓冲窗（秒）：只把'接近取消窗'的点击（窗前这么多秒内）记下、窗开即触发；更早的随手点会过期被忽视——避免玩家停手后陈旧输入还在续连招。\n0 = 不预输入（只认取消窗内 + 窗后宽限的点击）；0.15~0.25 较跟手。连点能连上靠后续点击不断刷新本窗。")]
    public float BufferWindow = 0.2f;
    [Tooltip("续接宽限窗（秒）：一招收招结束后保留连招状态这么久，期内点击继续接下一招而非从头起手。0.2~0.35 较好。\n解决'点击稍晚于取消窗 / 落在段末边界帧'被误判成重起第一段。")]
    public float ComboGraceWindow = 0.25f;
    [Tooltip("方向边判定阈值（移动意图 MoveWorld 模长）：超过才算玩家在按方向（Forward/Back），否则按 Any。做突进 / 回旋分支用。")]
    public float DirInputThreshold = 0.3f;

    [Header("招式图")]
    [Tooltip("招式节点。约定 [0]=Neutral 入口（Skill 留空，Links 为各键起手招）。")]
    public ComboNode[] Nodes;

    /// <summary>一个招式节点：要播的招式 + 它的后续出边。</summary>
    [Serializable]
    public class ComboNode
    {
        [Tooltip("编辑器可读标识，如 \"Neutral\" / \"Atk1\" / \"Finisher\"")]
        public string Name;
        [Tooltip("本节点播放的招式。Neutral 节点留空。")]
        public SkillDef Skill;
        [Tooltip("出边：在本招式取消窗内按对应键（+方向）→ 跳到 Nodes[TargetNode]。按数组顺序匹配，先中先用——把更特化的方向边排在 Any 边前面。")]
        public ComboLink[] Links;
    }

    /// <summary>一条连招转移：按键 (+ 方向条件) → 目标节点。</summary>
    [Serializable]
    public class ComboLink
    {
        [Tooltip("触发本边的攻击键")]
        public ComboButton Button = ComboButton.Light;
        [Tooltip("方向条件（相对角色当前朝向）：Any=不限；Forward=按住前向；Back=按住后向。做突进 / 回旋分支用。")]
        public ComboDir Direction = ComboDir.Any;
        [Tooltip("命中则跳到 Nodes[TargetNode]")]
        public int TargetNode;
    }
}

/// <summary>攻击键的语义分类（与具体按键解耦）。当前映射：Light=左键、Heavy=V。
/// 扩展只需加枚举值 + InputComponent 多 RaiseAttack 一行 + 图里加用到该键的边。</summary>
public enum ComboButton { Light, Heavy, Special, Special2 }

/// <summary>连招分支的方向条件（相对角色朝向，由 ComboComponent 按移动意图判定）。</summary>
public enum ComboDir { Any, Forward, Back }
