/// <summary>
/// UI 全局视觉主题。目前只承载「字体缩放」一项:
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
}