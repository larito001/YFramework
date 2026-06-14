using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宝箱 Actor 集合 + 每帧驱动。结构对照 <see cref="TowerManager"/>：维护 List + Tick + ActorWorld 注册 + 延迟 Remove。
/// 宝箱当前无组件（不动、不掉血），Tick 是廉价空转，保留是为了和其它 Actor 一致、便于以后加组件
/// （带锁/限时宝箱）。
///
/// 注意与 <see cref="ChestSystem"/> 的分工：ChestSystem 是**配表 + roll 工厂**（数据服务，不管世界实例）；
/// 本 Manager 管**世界宝箱 actor 的生命周期**（spawn / 注册 ActorWorld / 清理），二者解耦。
/// </summary>
public class ChestManager : IGameService, ITickable
{
    private GameContext ctx;
    private ViewManager viewMgr;
    private ActorWorld world;
    private ChestFactory factory;
    private readonly List<ChestActor> chests = new List<ChestActor>();
    // 组件 Tick 期间调 RemoveChest 不会改正在遍历的 chests 列表，攒到本帧 Tick 末统一清。
    private readonly List<ChestActor> toRemove = new List<ChestActor>();
    private bool ticking;

    public void Init(GameContext context)
    {
        ctx = context;
        viewMgr = context.Get<ViewManager>();
        world = context.Get<ActorWorld>();
        factory = new ChestFactory();
        factory.BindViewManager(viewMgr);
    }

    public void Shutdown()
    {
        for (int i = chests.Count - 1; i >= 0; i--)
        {
            var c = chests[i];
            world.Unregister(c.ID);
            viewMgr.RemoveBaseView(c.ID);
            c.Dispose();
        }
        chests.Clear();
        toRemove.Clear();
    }

    public void Tick(float dt)
    {
        ticking = true;
        try
        {
            for (int i = 0; i < chests.Count; i++)
                chests[i].Tick(dt);
        }
        finally { ticking = false; }

        if (toRemove.Count > 0)
        {
            for (int i = 0; i < toRemove.Count; i++) RemoveImmediate(toRemove[i]);
            toRemove.Clear();
        }
    }

    /// <summary>外部 API：在指定位置 spawn 一个宝箱。modelPath = 宝箱模型 prefab（Resources 相对路径）。</summary>
    public ChestActor SpawnChest(int chestId, string modelPath, Vector3 position, Quaternion rotation, float interactRange = 3.5f)
    {
        var c = factory.CreateChest(chestId, modelPath, position, rotation, interactRange);
        chests.Add(c);
        world.Register(c);
        return c;
    }

    /// <summary>请求移除宝箱（deferred）。Tick 中调用安全；实际清理发生在本帧 Tick 末尾。</summary>
    public void RemoveChest(ChestActor chest)
    {
        if (chest == null) return;
        if (ticking)
        {
            if (!toRemove.Contains(chest)) toRemove.Add(chest);
            return;
        }
        RemoveImmediate(chest);
    }

    private void RemoveImmediate(ChestActor chest)
    {
        if (chest == null) return;
        chests.Remove(chest);
        world.Unregister(chest.ID);
        viewMgr.RemoveBaseView(chest.ID);
        chest.Dispose();
    }
}
