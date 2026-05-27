using System.Collections.Generic;

/// <summary>
/// Character 集合 + 每帧驱动。所有 Character 的逻辑组件统一在这里 Tick。
/// </summary>
public class CharacterManager : ViewManager, ITickable
{
    private CharacterFactory factory;
    private readonly List<Character> characters = new List<Character>();

    public override void Init(GameContext ctx)
    {
        base.Init(ctx);
        factory = new CharacterFactory();
        factory.BindViewManager(this);
    }

    public override void Shutdown()
    {
        for (int i = characters.Count - 1; i >= 0; i--)
            characters[i].Dispose();
        characters.Clear();
        base.Shutdown();
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
        if (Views.TryGetValue(c.ID, out var view) && view != null)
        {
            var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
            if (ctx != null && ctx.TryGet<CameraManager>(out var cam))
                cam.SetFollow(view.transform);
        }
    }

    public void RemoveCharacter(Character character)
    {
        characters.Remove(character);
        character.Dispose();
        RemoveBaseView(character.ID);
    }
}
