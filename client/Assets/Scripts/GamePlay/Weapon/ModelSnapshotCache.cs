using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;
using Object = UnityEngine.Object;

/// <summary>
/// 道具卡片快照的全局共享缓存。把 <see cref="ModelSnapshot.CaptureAsync"/> 渲出的 3D 道具侧视快照按 modelPath 缓存,
/// 装备页 / 商城页 / 图鉴等卡片列表共用同一份——**同一把武器全局只渲一次**,跨面板、跨多次打开都复用。
///
/// **统一规格**(256 侧视 + 卡片底色),保证各页面卡片图视觉一致。卡片显示用 <see cref="CardSpriteAsync"/>:
/// 把快照包成 Sprite,配 <c>Image.preserveAspect=true</c> 即可按比例居中,不会被卡片图框拉伸(RawImage 无 preserveAspect)。
/// **生命周期**:进程级常驻(不随面板关闭释放),换取"渲过即复用";道具种类有限(数十),内存可控(~256KB/张)。
/// 需要主动释放可调 <see cref="Clear"/>(注意:释放后仍在显示这些图的 Image 会变空白)。
/// </summary>
public static class ModelSnapshotCache
{
    private static readonly Color CardBg = new Color(0.12f, 0.13f, 0.16f, 1f); // 与原装备页 SnapshotBg 一致

    private static readonly Dictionary<string, Texture2D> texCache = new();
    private static readonly Dictionary<string, Sprite> spriteCache = new();
    private static readonly Dictionary<string, List<Action<Sprite>>> spritePending = new(); // 同 modelPath 渲染进行中的等待者,合并并发

    /// <summary>异步取道具卡片快照的 Sprite(命中缓存含失败 null 立即回调;同一 modelPath 并发只渲一次,余者排队等同一结果)。
    /// 配 Image.preserveAspect 显示,避免拉伸。无模型/渲染失败回 null,由调用方回退 2D 图标。</summary>
    public static void CardSpriteAsync(string modelPath, ResMgr res, Action<Sprite> onReady)
    {
        if (onReady == null) return;
        if (string.IsNullOrEmpty(modelPath) || res == null) { onReady(null); return; }
        if (spriteCache.TryGetValue(modelPath, out var sp)) { onReady(sp); return; } // 含失败缓存的 null
        if (spritePending.TryGetValue(modelPath, out var waiters)) { waiters.Add(onReady); return; } // 并发合并

        spritePending[modelPath] = new List<Action<Sprite>> { onReady };
        ModelSnapshot.CaptureAsync(modelPath, res, tex =>
        {
            texCache[modelPath] = tex; // 含失败缓存的 null
            var sprite = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            spriteCache[modelPath] = sprite;

            if (spritePending.TryGetValue(modelPath, out var list))
            {
                spritePending.Remove(modelPath);
                foreach (var cb in list) cb(sprite);
            }
        }, 256, sideView: true, bg: CardBg);
    }

    /// <summary>把卡片图标异步绑定到 <paramref name="pic"/>:优先 3D 模型侧视快照,无模型/渲染失败回退 2D 图标(<paramref name="iconPath"/>),
    /// 两者都没有则隐藏。先占位隐藏再异步赋图;<paramref name="pic"/> 随卡片销毁(Unity 假 null)时回调自动丢弃。</summary>
    public static void BindCardImageAsync(Image pic, string modelPath, string iconPath, ResMgr res)
    {
        if (pic == null) return;
        pic.sprite = null;
        pic.enabled = false; // 先占位隐藏
        CardSpriteAsync(modelPath, res, sprite =>
        {
            if (pic == null) return;
            if (sprite != null) { pic.sprite = sprite; pic.enabled = true; return; }
            // 无模型快照:回退 2D 图标
            if (res == null || string.IsNullOrEmpty(iconPath)) { pic.enabled = false; return; }
            res.LoadAsync<Sprite>(iconPath, sp =>
            {
                if (pic == null) { if (sp != null) res.Release<Sprite>(iconPath); return; }
                pic.sprite = sp;
                pic.enabled = sp != null;
            });
        });
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
