using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character 集合 + 每帧驱动。所有 Character 的逻辑组件统一在这里 Tick。
/// 只管 Actor 列表 + 生命周期；view 加载/销毁委托给全局 ViewManager service。
/// </summary>
public class CharacterManager : IGameService, ITickable
{
    private GameContext ctx;
    private ViewManager viewMgr;
    private ActorWorld world;
    private CharacterFactory factory;
    private readonly List<Character> characters = new List<Character>();

    public void Init(GameContext context)
    {
        ctx = context;
        viewMgr = context.Get<ViewManager>();
        world = context.Get<ActorWorld>();
        factory = new CharacterFactory();
        factory.BindViewManager(viewMgr);
    }

    public void Shutdown()
    {
        for (int i = characters.Count - 1; i >= 0; i--)
        {
            var c = characters[i];
            world.Unregister(c.ID);
            viewMgr.RemoveBaseView(c.ID);
            c.Dispose();
        }
        characters.Clear();
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < characters.Count; i++)
            characters[i].Tick(dt);
    }

    public void GenneratePlayer(Vector3 position = default)
    {
        var c = factory.CreateCharacter(position);
        characters.Add(c);
        world.Register(c);

        // 让相机跟随玩家。view 由 LoadBaseView 在 factory 内同步创建，这里能拿到。
        if (viewMgr.TryGetView(c.ID, out var view) && view != null)
        {
            if (ctx != null && ctx.TryGet<CameraManager>(out var cam))
                cam.SetFollow(view.transform);
        }

        // 测试用：在玩家前方铺 3 个 Dummy 站桩靶，便于验证近战/子弹/导弹/射线四种伤害源都能扣血。
        // 不想要就删这三行。
        SpawnDummy(new Vector3(0f, 0f, 5f));
        SpawnDummy(new Vector3(2f, 0f, 6f));
        SpawnDummy(new Vector3(-2f, 0f, 6f));
    }

    /// <summary>生成一个站桩 Dummy 敌人在指定位置。返回 Character 实例供外部进一步配置（订阅 OnDied 等）。</summary>
    public Character SpawnDummy(Vector3 position, float maxHealth = 100f)
    {
        var c = factory.CreateDummy(position, maxHealth);
        characters.Add(c);
        world.Register(c);
        return c;
    }

    public void RemoveCharacter(Character character)
    {
        characters.Remove(character);
        world.Unregister(character.ID);
        viewMgr.RemoveBaseView(character.ID);
        character.Dispose();
    }
}
