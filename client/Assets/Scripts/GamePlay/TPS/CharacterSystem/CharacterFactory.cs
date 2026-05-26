/// <summary>
/// 创建 Character：new Character + 让 ViewManager 完成 prefab 加载/实例化/Bind/注册。
/// 不直接持有 CharacterManager，避免反向依赖；ViewManager 抽象足以满足创建需求。
/// </summary>
public class CharacterFactory
{
    private ViewManager manager;

    public void BindViewManager(ViewManager manager)
    {
        this.manager = manager;
    }

    public Character CreateCharacter()
    {
        var character = new Character();
        manager.LoadBaseView<CharacterView>("Player/Player", character);
        return character;
    }
}
