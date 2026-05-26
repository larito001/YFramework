
/// <summary>
/// 角色的view，
/// </summary>
public class CharacterView : BaseView
{
    Character character;
    public override void Bind(Actor actor,int id)
    {
        character = actor as Character;
        ID = id;
    }
}
