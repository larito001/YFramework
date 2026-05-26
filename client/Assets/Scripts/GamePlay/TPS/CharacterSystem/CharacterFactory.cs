using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建角色，绑定view和Character：Character player = factory.Create();
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
        var Obj = manager.LoadBaseView("playerPath").GetComponent<CharacterView>();
        Obj.Bind(character,character.ID);
        return character;
    }
}