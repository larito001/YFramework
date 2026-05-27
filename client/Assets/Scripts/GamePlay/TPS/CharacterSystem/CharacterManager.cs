using System.Collections.Generic;

/// <summary>
/// Character 集合 + 每帧驱动。所有 Character 的逻辑组件统一在这里 Tick。
/// 只管 Actor 列表 + 生命周期；view 加载/销毁委托给全局 ViewManager service。
/// </summary>
public class CharacterManager : IGameService, ITickable
{
    private GameContext ctx;
    private ViewManager viewMgr;
    private CharacterFactory factory;
    private readonly List<Character> characters = new List<Character>();

    public void Init(GameContext context)
    {
        ctx = context;
        viewMgr = context.Get<ViewManager>();
        factory = new CharacterFactory();
        factory.BindViewManager(viewMgr);
    }

    public void Shutdown()
    {
        for (int i = characters.Count - 1; i >= 0; i--)
        {
            viewMgr.RemoveBaseView(characters[i].ID);
            characters[i].Dispose();
        }
        characters.Clear();
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < characters.Count; i++)
            characters[i].Tick(dt);
    }

    public void GenneratePlayer()
    {
        var c = factory.CreateCharacter();
        characters.Add(c);

        // 让相机跟随玩家。view 由 LoadBaseView 在 factory 内同步创建，这里能拿到。
        if (viewMgr.TryGetView(c.ID, out var view) && view != null)
        {
            if (ctx != null && ctx.TryGet<CameraManager>(out var cam))
                cam.SetFollow(view.transform);
        }
    }

    public void RemoveCharacter(Character character)
    {
        characters.Remove(character);
        character.Dispose();
        viewMgr.RemoveBaseView(character.ID);
    }
}
