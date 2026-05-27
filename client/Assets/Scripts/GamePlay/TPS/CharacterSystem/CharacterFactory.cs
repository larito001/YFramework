using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创建 Character：new Character + 装组件 + ViewManager 加载 prefab。
/// view 被动消费 Character 数据，组件添加顺序与 LoadBaseView 无依赖关系。
/// 组件 Tick 顺序按 Add 顺序：Aim 在 Move 之前（Move 反算 local 动画依赖 Aim 写入的 Rotation）。
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
        character.Add(new AimComponent());
        character.Add(new MoveComponent());
        character.Add(new WeaponComponent
        {
            Weapons = new List<Weapon>
            {
                // 没有 Pistol 动画，两个槽都用同一份 Animator Controller，只换模型 + 播切枪过场
                new Weapon
                {
                    Name = "Rifle A",
                    ModelPath = "Weapon/RiflePlaceholder",
                    LocalPosition = Vector3.zero,
                    LocalEuler = Vector3.zero,
                },
                new Weapon
                {
                    Name = "Rifle B",
                    ModelPath = "Weapon/PistolPlaceholder",
                    LocalPosition = Vector3.zero,
                    LocalEuler = Vector3.zero,
                },
            },
        });
        manager.LoadBaseView<CharacterView>("Player/Player", character);
        return character;
    }
}
