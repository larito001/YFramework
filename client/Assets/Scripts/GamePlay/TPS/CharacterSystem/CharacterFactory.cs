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
                // slot 0：步枪。没 AnimOverride 沿用 playerController（Rifle 系列动画）
                new Weapon
                {
                    Name = "Rifle",
                    ModelPath = "Weapon/RiflePlaceholder",
                    LocalPosition = Vector3.zero,
                    LocalEuler = Vector3.zero,
                },
                // slot 1：手枪。项目里没 Pistol 动画包，先共用 Rifle 动画作占位，
                // 视觉只换 RightHandProp 上的模型；想要单手姿势等导入 Pistol 动画后做 AnimOverride
                new Weapon
                {
                    Name = "Pistol",
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
