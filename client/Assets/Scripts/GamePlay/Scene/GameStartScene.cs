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
        // 旧 TPS 角色系统已移除:不再生成玩家,也不注册依赖玩家血量的物品使用逻辑。
        // 后续接入新(2D)角色系统后,在此恢复玩家生成与按物品 id 注册的使用处理器。
        SpawnChests(); // 在场景原点周围随机散布三种品质的宝箱
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

    /// <summary>在玩家出生点周围随机散布 <see cref="ChestCount"/> 个宝箱,每个随机选一种品质。</summary>
    private void SpawnChests()
    {
        var res = Context.Get<YOTO.ResMgr>();
        Vector3 center = Vector3.zero; // 旧玩家出生点参照已移除,暂以场景原点为中心

        for (int i = 0; i < ChestCount; i++)
        {
            var kind = ChestKinds[Random.Range(0, ChestKinds.Length)]; // 随机品质
            var prefab = res.Load<GameObject>(kind.prefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameStartScene] 找不到宝箱模型 {kind.prefab}");
                continue;
            }

            Vector3 pos = RandomGroundPos(center) + Vector3.up * ChestYOffset; // y 抬高 0.5
            var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var chest = go.GetComponent<ChestEntity>();
            if (chest == null) chest = go.AddComponent<ChestEntity>();
            chest.chestId = kind.chestId;
            chest.interactRange = 3.5f;
        }
    }

    /// <summary>出生点周围环形随机点(xz 平面,[ChestMinDistance, ChestSpawnRadius])。</summary>
    private Vector3 RandomGroundPos(Vector3 center)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(ChestMinDistance, ChestSpawnRadius);
        return center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
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
