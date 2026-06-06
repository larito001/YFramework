using UnityEngine;

/// <summary>
/// 道具品质 → 品质框颜色。品质取自 item 配表 <c>quality</c> 列(0~5),归到「浅蓝 / 紫 / 金 / 红」四档。
/// 品质框图用美术九宫格 <c>CardFrame_02_White_Bg</c>,按此色染;道具快照背景设为透明,框色即 icon 背景。
/// 想调配色只改这一处,商城/装备卡共用。
/// </summary>
public static class ItemQualityPalette
{
    public const string FrameSpritePath =
        "Assets/Art/UI/NewUI/Shared/Sprite_Common/Frame/CardFrame/CardFrame_02_White_Bg.png";

    private static readonly Color[] ByQuality =
    {
        new Color(0.50f, 0.74f, 0.96f, 1f), // 0 浅蓝
        new Color(0.50f, 0.74f, 0.96f, 1f), // 1 浅蓝
        new Color(0.70f, 0.48f, 0.88f, 1f), // 2 紫
        new Color(0.96f, 0.80f, 0.32f, 1f), // 3 金
        new Color(0.92f, 0.42f, 0.42f, 1f), // 4 红
        new Color(0.92f, 0.42f, 0.42f, 1f), // 5 红
    };

    public static Color FrameColor(uint quality)
    {
        int q = quality < (uint)ByQuality.Length ? (int)quality : 0;
        return ByQuality[q];
    }
}
