using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局 Actor 注册表，按 ID 索引所有活动 Actor。
/// 各 Manager 在 spawn 完 actor 后 Register，despawn 前 Unregister。
/// 跨 Actor 引用（组件找别的 actor、view 找 owner 数据）统一走这里 —— 不要持有 Actor 引用，
/// 否则 actor 销毁后引用悬空，发现不了。
///
/// 不接管 Tick：每类 Manager 仍维护自己的列表 + Tick 顺序（Character 先于 Weapon 先于 Bullet 这种 ordering 留在 Manager）。
/// </summary>
public class ActorWorld : IGameService
{
    private readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();

    public void Init(GameContext ctx) { }

    public void Shutdown() { _actors.Clear(); }

    public void Register(Actor actor)
    {
        if (actor == null) return;
        if (_actors.ContainsKey(actor.ID))
        {
            Debug.LogError($"[ActorWorld] 重复注册 actor id={actor.ID}");
            return;
        }
        _actors[actor.ID] = actor;
    }

    public void Unregister(int id) => _actors.Remove(id);

    public bool TryGet(int id, out Actor actor) => _actors.TryGetValue(id, out actor);

    /// <summary>按 id + 类型一步查 actor。找不到或类型不匹配返回 null。</summary>
    public T Get<T>(int id) where T : Actor
    {
        return _actors.TryGetValue(id, out var a) ? a as T : null;
    }

    public IReadOnlyDictionary<int, Actor> All => _actors;

    /// <summary>把当前所有 Actor **追加**到 buffer（不清空 buffer，调用方自己 Clear）。供需要遍历的场景用，如塔 AI 扫敌。
    /// 通过外部 buffer 复用避免每帧 alloc；遍历内部 Dictionary 直接走 struct enumerator 也不 alloc。
    /// 命名 Append 而非 Get 是因为不替换 buffer 内容——和 Actor.AppendAll&lt;T&gt; 保持一致。</summary>
    public void AppendAll(List<Actor> buffer)
    {
        if (buffer == null) return;
        foreach (var kv in _actors) buffer.Add(kv.Value);
    }
}
