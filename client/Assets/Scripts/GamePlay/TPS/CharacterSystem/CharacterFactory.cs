/// <summary>
/// 创建 Character：new Character + 装组件 + ViewManager 加载 prefab。
/// view 被动消费 Character 数据，组件添加顺序与 LoadBaseView 无依赖关系。
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
        character.Add(new MoveComponent());
        manager.LoadBaseView<CharacterView>("Player/Player", character);
        return character;
    }
}
