/// <summary>角色相关资源的 Resources 相对路径集中处。
///
/// **资源目录调整时只改 <see cref="PlayerRoot"/> / <see cref="ZombieRoot"/> 两个前缀**，
/// 下面的具体路径全靠拼接自动跟着变，运行时（<see cref="CharacterFactory"/>）和编辑器生成器
/// （ZombieAssetBuilder / ComboAssetBuilder）都引用这里的常量，不用再满项目改字符串。
///
/// 约定：这些是 <c>Resources.Load</c> 用的**相对路径**（不含 "Assets/Resources/" 前缀、不含扩展名）。
/// 编辑器写盘要的绝对路径用 <see cref="ToAsset"/> 拼成 "Assets/Resources/xxx.asset"。
/// </summary>
public static class CharacterResPath
{
    /// <summary>玩家资源根前缀（Resources 相对，末尾带 /）。当前：Character/Player/。</summary>
    public const string PlayerRoot = "Character/Player/";
    /// <summary>僵尸资源根前缀（Resources 相对，末尾带 /）。当前：Character/Zombie/。</summary>
    public const string ZombieRoot = "Character/Zombie/";

    // —— 玩家 ——
    public const string PlayerPrefab  = PlayerRoot + "Prefabs/Player";
    public const string PlayerAnimSet = PlayerRoot + "Animations/PlayerAnimSet";
    /// <summary>翻滚手感配置（DodgeConfig 资产）。菜单 Assets/Create/TPS/Dodge Config 创建，放到
    /// Resources/Character/Player/Config/DodgeConfig.asset。缺失则 DodgeComponent 用内置默认。</summary>
    public const string PlayerDodgeConfig = PlayerRoot + "Config/DodgeConfig";
    public const string PlayerMelee   = PlayerRoot + "Skills/PlayerMelee";
    public const string PlayerKnife   = PlayerRoot + "Skills/PlayerKnife";
    public const string PlayerKnifeV  = PlayerRoot + "Skills/PlayerKnifeV";
    public const string KnifeCombo    = PlayerRoot + "Skills/KnifeCombo";

    // —— 僵尸 ——
    public const string ZombiePrefab  = ZombieRoot + "Prefabs/Zombie";
    public const string ZombieAnimSet = ZombieRoot + "Animations/ZombieAnimSet";
    public const string ZombieAttack  = ZombieRoot + "Skills/ZombieAttack";
    public const string ZombieLeap    = ZombieRoot + "Skills/ZombieLeap";

    /// <summary>把 Resources 相对路径拼成编辑器写盘用的工程绝对路径，如
    /// <c>"Character/Player/Skills/PlayerMelee" → "Assets/Resources/Character/Player/Skills/PlayerMelee.asset"</c>。</summary>
    public static string ToAsset(string resourcesRelativePath, string extension = ".asset")
        => "Assets/Resources/" + resourcesRelativePath + extension;
}