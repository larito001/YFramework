using UnityEngine;

/// <summary>
/// UI 全局视觉主题。承载「字体缩放」与「主题色」:
/// 所有 UI 文本的字号都以「基准字号 × <see cref="FontScale"/>」落地,想整体放大/缩小或调回原样,只改这一个数。
/// 运行时面板(<c>GamePlay/UI/*</c>)与 Editor 预制体构建器(<c>Editor/UI/*Builder</c>)都统一走 <see cref="Font"/>;
/// 改了系数后,运行时面板即时生效,而 Builder 生成的预制体需在 Unity 重跑 <c>Tools/UI/Build *** Prefab</c> 重新生成才会更新。
/// </summary>
public static class UITheme
{
    /// <summary>全局字体缩放系数。1 = 原样,2 = 放大一倍。所有字号赋值统一乘它。</summary>
    public const float FontScale = 2f;

    /// <summary>把「基准字号」换算成「实际字号」(乘全局缩放)。所有 UI 文本字号都应经由这里赋值。</summary>
    public static float Font(float baseSize) => baseSize * FontScale;

    // ===== 全局主题色(草绿主题,与 logo「打猎模拟器」一致)。改主题色只动这里。 =====
    /// <summary>主题主色:logo 草绿。选中态 / 主按钮 / 强调元素用。</summary>
    public static readonly Color Primary = new Color(0.45f, 0.72f, 0.20f, 1f);
    /// <summary>主色的浅色版(选中态高亮 / 浅底)。</summary>
    public static readonly Color PrimaryLight = new Color(0.62f, 0.82f, 0.42f, 1f);
    /// <summary>主题黄(logo「模拟器」的黄):分割线 / 强调用。</summary>
    public static readonly Color PrimaryYellow = new Color(1f, 0.888f, 0f, 1f);
    /// <summary>页面背景底色(浅绿)。bg.prefab 染色与各页 RootBg 统一用它。</summary>
    public static readonly Color Background = new Color(0.80f, 0.88f, 0.68f, 1f);
    /// <summary>未选中态(灰绿)。</summary>
    public static readonly Color Inactive = new Color(0.74f, 0.78f, 0.66f, 1f);

    /// <summary>弹窗遮罩(压暗身后画面):浅米白 F8F7F2,半透明。所有弹窗统一用它。</summary>
    public static readonly Color Scrim = new Color(0.972549f, 0.968627f, 0.94902f, 0.62f);
    /// <summary>内容底板(列表/卡片后的底,叠在美术面板图上):浅米白 F8F7F2。</summary>
    public static readonly Color PanelBacking = new Color(0.972549f, 0.968627f, 0.94902f, 0.9f);
}