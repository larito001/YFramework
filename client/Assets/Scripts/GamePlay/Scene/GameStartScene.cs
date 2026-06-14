using UnityEngine;

public class GameStartScene : YSceneBase
{
    public override YSceneType SceneType
    {
        get { return YSceneType.Home; }
    }

    public override string SceneName
    {
        get { return "GameStartScene"; }
    }

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        UI.Hide<StartPanel>();
        var manager = Context.Get<CharacterManager>();
        manager.GenneratePlayer();

        RegisterItemUseHandlers(); // 物品使用逻辑(gameplay 内容,放场景而非组装层)
        SpawnChests();             // 玩家附近随机散布三种品质的宝箱

        // 【测试用】玩家正前方放一个局部时间缩放圈：站进去角色变 0.5 倍速，全局保持 1 倍。不想要删这行。
        TimeScaleZone.Spawn(GetPlayerSpawnPos() + new Vector3(0f, 0f, 5f), radius: 3f, insideScale: 0.1f);
    }

    /// <summary>注册各消耗品的使用逻辑(按物品 id 绑处理器)。处理器内部延迟查找玩家,注册时机不要求玩家已就绪。</summary>
    private void RegisterItemUseHandlers()
    {
        var bag = Context.Get<BagSystem>();
        bag.RegisterUseHandler(1001, new HealItemUseHandler(Context, 30f));  // 小型治疗药水
        bag.RegisterUseHandler(1002, new HealItemUseHandler(Context, 80f));  // 大型治疗药水
    }

    // ---------------- 宝箱刷新 ----------------

    /// <summary>三种宝箱品质 → 模型预制体 + chest 配表 id。三个预制体各对应一种品质。</summary>
    private static readonly (string prefab, int chestId)[] ChestKinds =
    {
        ("SceneObj/Box/ammo_box_01", 1), // 普通:木宝箱
        ("SceneObj/Box/ammo_box_02", 2), // 稀有:铁宝箱
        ("SceneObj/Box/ammo_box_03", 3), // 史诗:黄金宝箱
    };

    private const int ChestCount = 6;      // 散布数量
    private const float ChestSpawnRadius = 12f; // 玩家出生点周围散布半径(米)
    private const float ChestMinDistance = 4f;  // 离出生点最小距离(避免压在玩家身上)
    private const float ChestYOffset = 0.5f;    // 模型抬高(y 轴向上 0.5 米)

    /// <summary>在玩家出生点周围随机散布 <see cref="ChestCount"/> 个宝箱,每个随机选一种品质。
    /// 走 <see cref="ChestManager.SpawnChest"/> 把宝箱纳入 Actor/View 体系（同角色/塔），不再手挂 MonoBehaviour。</summary>
    private void SpawnChests()
    {
        var chestMgr = Context.Get<ChestManager>();
        Vector3 center = GetPlayerSpawnPos();

        for (int i = 0; i < ChestCount; i++)
        {
            var kind = ChestKinds[Random.Range(0, ChestKinds.Length)]; // 随机品质
            Vector3 pos = RandomGroundPos(center) + Vector3.up * ChestYOffset; // y 抬高 0.5
            var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            chestMgr.SpawnChest(kind.chestId, kind.prefab, pos, rot, interactRange: 3.5f);
        }
    }

    /// <summary>出生点周围环形随机点(xz 平面,[ChestMinDistance, ChestSpawnRadius])。</summary>
    private Vector3 RandomGroundPos(Vector3 center)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(ChestMinDistance, ChestSpawnRadius);
        return center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    /// <summary>玩家当前世界坐标(取不到则用原点)。</summary>
    private Vector3 GetPlayerSpawnPos()
    {
        var charMgr = Context.Get<CharacterManager>();
        var player = charMgr != null ? charMgr.Player : null;
        if (player == null) return Vector3.zero;
        if (Context.TryGet<ViewManager>(out var vm) && vm.TryGetView(player.ID, out var view) && view != null)
            return view.transform.position;
        return player.Position;
    }

    protected override void OnEnterScene()
    {  
        EnterSceneComplete();
    }
    protected override void OnLeaveScene()
    {
        LeaveSceneComplete();
    }
}
