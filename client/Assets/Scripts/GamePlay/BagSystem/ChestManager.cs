using UnityEngine;

/// <summary>
/// 宝箱 Actor 集合管理：复用 <see cref="ActorManager{T}"/> 的 List + ActorWorld 注册 + 清理骨架，本类只管「怎么 spawn 一个宝箱」。
/// 宝箱无逻辑组件（不动、不掉血），**不实现 ITickable**——不进 tick 调度、零空转。日后若加组件（带锁/限时宝箱）
/// 再声明 ITickable 并在 Tick 里调基类 TickActors 即可。
///
/// 与 <see cref="ChestSystem"/> 的分工：ChestSystem = 配表 + roll 工厂（数据服务）；本 Manager 管世界宝箱 actor 的生命周期。
/// </summary>
public class ChestManager : ActorManager<ChestActor>
{
    private ChestFactory factory;

    protected override void OnInit()
    {
        factory = new ChestFactory();
        factory.BindViewManager(ViewMgr);
    }

    /// <summary>外部 API：在指定位置 spawn 一个宝箱。modelPath = 宝箱模型 prefab（Resources 相对路径）。</summary>
    public ChestActor SpawnChest(int chestId, string modelPath, Vector3 position, Quaternion rotation, float interactRange = 3.5f)
    {
        var c = factory.CreateChest(chestId, modelPath, position, rotation, interactRange);
        Track(c);
        return c;
    }
}
