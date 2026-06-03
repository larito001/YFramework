using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 道具卡片快照的全局共享缓存。把 <see cref="ModelSnapshot.Capture"/> 渲出的 3D 道具侧视快照按 modelPath 缓存,
/// 装备页 / 商城页 / 图鉴等卡片列表共用同一份——**同一把武器全局只渲一次**,跨面板、跨多次打开都复用。
///
/// **统一规格**(256 侧视 + 卡片底色),保证各页面卡片图视觉一致。卡片显示用 <see cref="CardSprite"/>:
/// 把快照包成 Sprite,配 <c>Image.preserveAspect=true</c> 即可按比例居中,不会被卡片图框拉伸(RawImage 无 preserveAspect)。
/// **生命周期**:进程级常驻(不随面板关闭释放),换取"渲过即复用";道具种类有限(数十),内存可控(~256KB/张)。
/// 需要主动释放可调 <see cref="Clear"/>(注意:释放后仍在显示这些图的 Image 会变空白)。
/// </summary>
public static class ModelSnapshotCache
{
    private static readonly Color CardBg = new Color(0.12f, 0.13f, 0.16f, 1f); // 与原装备页 SnapshotBg 一致

    private static readonly Dictionary<string, Texture2D> texCache = new();
    private static readonly Dictionary<string, Sprite> spriteCache = new();

    /// <summary>取道具卡片侧视快照贴图(命中即复用;未命中渲一次并缓存,失败的 null 也缓存避免反复重试)。</summary>
    public static Texture2D CardSnapshot(string modelPath, ResMgr res)
    {
        if (string.IsNullOrEmpty(modelPath) || res == null) return null;
        if (texCache.TryGetValue(modelPath, out var tex)) return tex; // 含失败缓存的 null
        tex = ModelSnapshot.Capture(modelPath, res, 256, sideView: true, bg: CardBg);
        texCache[modelPath] = tex;
        return tex;
    }

    /// <summary>取道具卡片快照的 Sprite(包住缓存贴图,本身也缓存)。配 Image.preserveAspect 显示,避免拉伸。无模型回 null。</summary>
    public static Sprite CardSprite(string modelPath, ResMgr res)
    {
        if (string.IsNullOrEmpty(modelPath) || res == null) return null;
        if (spriteCache.TryGetValue(modelPath, out var sp)) return sp;
        var tex = CardSnapshot(modelPath, res);
        sp = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
        spriteCache[modelPath] = sp;
        return sp;
    }

    /// <summary>释放所有已缓存的快照 Sprite 与贴图并清空(下次取再重渲)。</summary>
    public static void Clear()
    {
        foreach (var s in spriteCache.Values) if (s != null) Object.Destroy(s);
        foreach (var t in texCache.Values) if (t != null) Object.Destroy(t);
        spriteCache.Clear();
        texCache.Clear();
    }
}
