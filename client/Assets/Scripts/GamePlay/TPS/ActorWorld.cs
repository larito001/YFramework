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
}
