using UnityEngine;

/// <summary>
/// 创建 ChestActor：new ChestActor + ViewManager 加载宝箱模型 prefab（addIfMissing 运行时挂 <see cref="ChestView"/>）。
/// 对照 <see cref="TowerFactory"/>。宝箱没有移动/HP/AI，暂不装任何组件——纯数据 + view，预留以后加组件
/// （如带锁宝箱、限时宝箱）的位置。
///
/// 模型 prefab 路径由调用方传入（不同品质对应不同模型，如 SceneObj/Box/ammo_box_01），
/// 走 ViewManager.LoadBaseView（ResMgr.Load + 引用计数），prefab 上没 ChestView 时 addIfMissing 运行时补。
/// </summary>
public class ChestFactory
{
    private ViewManager manager;

    public void BindViewManager(ViewManager manager)
    {
        this.manager = manager;
    }

    /// <summary>创建宝箱。modelPath = 宝箱模型 prefab（Resources 相对路径，不带扩展名）。</summary>
    public ChestActor CreateChest(int chestId, string modelPath, Vector3 position, Quaternion rotation, float interactRange = 3.5f)
    {
        var chest = new ChestActor { ChestId = chestId, InteractRange = interactRange };

        // addIfMissing：宝箱模型 prefab 一般不挂 ChestView，运行时补一个。
        var view = manager.LoadBaseView<ChestView>(modelPath, chest, addIfMissing: true);
        if (view != null)
        {
            view.gameObject.name = $"Chest_{chest.ID}";
            // Bind 内用 instantiate 时的默认 transform 给 chest.Position 播种，这里再覆盖成目标摆位
            // （顺序同 TowerFactory：LoadBaseView 后再设 transform + actor.Position）。
            view.transform.position = position;
            view.transform.rotation = rotation;
        }
        chest.Position = position;
        chest.Rotation = rotation;
        return chest;
    }
}
