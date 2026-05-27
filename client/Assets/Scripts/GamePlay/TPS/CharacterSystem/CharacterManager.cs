using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : ViewManager
{
    CharacterFactory factory;
    List<Character> characters;

    public override void Init(GameContext ctx)
    {
        base.Init(ctx);
        base.Init(ctx);
        factory = new CharacterFactory();
        factory.BindViewManager(this);
        characters = new List<Character>();
    }

    public void Shutdown()
    {
        base.Shutdown();
    }

    public void GenneratePlayer()
    {
        var c = factory.CreateCharacter();
        characters.Add(c);
    }

    public void RemoveCharacter(Character character)
    {
        characters.Remove(character);
        RemoveBaseView(character.ID);
    }
}