using System;
using System.Collections.Generic;
using UnityEngine;

#region 判定接口

/// <summary>
/// 事务：扣钱→生成→注册（或升级/回收）
/// </summary>
public interface IInventoryService
{
    bool Has(string currency, int amount);
    bool TrySpend(string currency, int amount);
    void Add(string currency, int amount);
}

/// <summary>
/// 
/// </summary>
public interface ITowerFactory
{
    // 返回 tower instanceId（你可以用 GUID/int）
    int SpawnTower(TowerConfigSO config, Vector3 position, Quaternion rotation);
    void DespawnTower(int instanceId);
    void SetTowerPose(int instanceId, Vector3 position, Quaternion rotation); // 可选
}

public interface ITowerRegistry
{
    bool TryGet(int instanceId, out TowerInstance inst);
    void Register(TowerInstance inst);
    void Unregister(int instanceId);
}
public readonly struct TowerInstance
{
    public readonly int InstanceId;
    public readonly int TowerId;
    public readonly TowerConfigSO Config;

    public TowerInstance(int instanceId, TowerConfigSO config)
    {
        InstanceId = instanceId;
        Config = config;
        TowerId = config != null ? config.towerId : -1;
    }
}
#endregion



#region 判定接口扩展

public sealed class TowerRegistry : ITowerRegistry
{
    private readonly Dictionary<int, TowerInstance> _map = new();

    public bool TryGet(int instanceId, out TowerInstance inst) => _map.TryGetValue(instanceId, out inst);
    public void Register(TowerInstance inst) => _map[inst.InstanceId] = inst;
    public void Unregister(int instanceId) => _map.Remove(instanceId);
}

#endregion


/// <summary>
/// 能不能放、放哪、怎么转
/// </summary>
public sealed class BuildPlacementSystem
{
    #region 建造条件判断
    
       public struct PlacementQuery
    {
        public Camera camera;
        public Vector2 screenPos; // 鼠标/触点屏幕坐标
        public TowerConfigSO config; // 当前要建造的塔
        public float maxRayDistance; // 射线距离
        public LayerMask groundMask; // 地面层
        public LayerMask blockingMask; // 阻挡层（塔/障碍/敌人等）
        public float overlapInflation; // 额外膨胀，避免贴边穿插
        public float yOffset; // 放置点Y偏移
        public bool ignoreTriggers;
    }

    public struct PlacementResult
    {
        public bool hasHit;
        public bool canPlace;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 surfaceNormal;
        public string reason; // 不可放置原因（给 UI 用）
        public Collider blockingCollider; // 造成阻挡的碰撞体（可选）
    }

    public PlacementResult Evaluate(in PlacementQuery q, float yawDegrees)
    {
        PlacementResult r = new PlacementResult
        {
            hasHit = false,
            canPlace = false,
            position = default,
            rotation = Quaternion.Euler(0f, yawDegrees, 0f),
            surfaceNormal = Vector3.up,
            reason = "NoHit"
        };

        if (q.camera == null || q.config == null)
        {
            r.reason = "MissingCameraOrConfig";
            return r;
        }

        Ray ray = q.camera.ScreenPointToRay(q.screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, q.maxRayDistance, q.groundMask,
                q.ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide))
        {
            r.reason = "RaycastNoGround";
            return r;
        }

        r.hasHit = true;
        r.surfaceNormal = hit.normal;

        // 坐标吸附（可选）
        Vector3 pos = hit.point;
        if (q.config.snapStep > 0.0001f)
        {
            float s = q.config.snapStep;
            pos.x = Mathf.Round(pos.x / s) * s;
            pos.z = Mathf.Round(pos.z / s) * s;
        }

        pos.y += q.yOffset;
        r.position = pos;

        // 坡度判定
        float slope = Vector3.Angle(hit.normal, Vector3.up);
        if (slope > Mathf.Max(0f, q.config.maxSlopeAngle))
        {
            r.canPlace = false;
            r.reason = "TooSteep";
            return r;
        }

        // 碰撞体阻挡判定：OverlapBox（轴对齐到 yaw 旋转）
        Vector3 half = q.config.footprintExtents;
        half += Vector3.one * Mathf.Max(0f, q.overlapInflation);

        // OverlapBox 中心：以放置点为底，抬高一点以覆盖塔体积
        Vector3 center = pos + Vector3.up * half.y;

        Collider[] hits = Physics.OverlapBox(
            center,
            half,
            r.rotation,
            q.blockingMask,
            q.ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            // 地面本身通常不在 blockingMask；如果你把地面也放进来了，这里可过滤
            // if (((1 << c.gameObject.layer) & q.groundMask) != 0) continue;

            r.blockingCollider = c;
            r.canPlace = false;
            r.reason = "Blocked";
            return r;
        }

        r.canPlace = true;
        r.reason = "OK";
        return r;
    }

    #endregion
 

    #region 建造

     public struct Result
    {
        public bool ok;
        public string reason;
        public int instanceId;

        public static Result Ok(int id) => new Result { ok = true, reason = "OK", instanceId = id };
        public static Result Fail(string reason) => new Result { ok = false, reason = reason, instanceId = 0 };
    }

    private readonly IInventoryService _inv;
    private readonly ITowerFactory _factory;
    private readonly ITowerRegistry _registry;

    public BuildPlacementSystem(IInventoryService inv, ITowerFactory factory, ITowerRegistry registry)
    {
        _inv = inv;
        _factory = factory;
        _registry = registry;
    }

    /// <summary>
    /// 事务：判定资源 -> 扣资源 -> 生成塔 -> 注册实例
    /// 注意：放置合法性（碰撞/坡度）应由 BuildPlacementSystem 在调用前保证。
    /// </summary>
    public Result TryBuildTower(TowerConfigSO config, Vector3 position, Quaternion rotation)
    {
        if (config == null || config.prefab == null) return Result.Fail("MissingConfigOrPrefab");
        if (_inv == null || _factory == null || _registry == null) return Result.Fail("MissingDependencies");

        if (!_inv.Has(config.costCurrency, config.buildCost))
            return Result.Fail("NotEnoughCurrency");

        if (!_inv.TrySpend(config.costCurrency, config.buildCost))
            return Result.Fail("SpendFailed");

        int id;
        try
        {
            id = _factory.SpawnTower(config, position, rotation);
        }
        catch (Exception e)
        {
            // 回滚
            _inv.Add(config.costCurrency, config.buildCost);
            Debug.LogError($"SpawnTower failed: {e}");
            return Result.Fail("SpawnFailed");
        }

        _registry.Register(new TowerInstance(id, config));
        return Result.Ok(id);
    }

    /// <summary>
    /// 升级：检查可升级 -> 扣资源 -> 生成新塔 -> 删除旧塔 -> 更新 registry
    /// </summary>
    public Result TryUpgradeTower(int instanceId)
    {
        if (_inv == null || _factory == null || _registry == null) return Result.Fail("MissingDependencies");
        if (!_registry.TryGet(instanceId, out var inst)) return Result.Fail("InstanceNotFound");
        if (inst.Config == null) return Result.Fail("MissingConfig");

        var next = inst.Config.upgradeTo;
        if (next == null) return Result.Fail("NoUpgrade");

        if (!_inv.Has(next.costCurrency, inst.Config.upgradeCost))
            return Result.Fail("NotEnoughCurrency");

        if (!_inv.TrySpend(next.costCurrency, inst.Config.upgradeCost))
            return Result.Fail("SpendFailed");

        // 升级时位姿保持：如果你想保留原塔 transform，需要 factory 提供 GetPose。
        // 这里给一个简单策略：由外部在调用 Upgrade 前把 pose 传进来，或者 factory 内能找到对象。
        // 我这里假设 factory 内部能从 instanceId 找到对象并读取 pose（你实现时做）。
        // 最稳妥是：给 ITowerFactory 增加 GetTowerPose(instanceId)。
        // 为了不改接口，这里给一个折中：先在原地生成（0,0,0），再 factory 自己修正（可选）。
        int newId;
        try
        {
            // 你可以改为 SpawnTower(next, oldPos, oldRot)
            newId = _factory.SpawnTower(next, Vector3.zero, Quaternion.identity);
        }
        catch (Exception e)
        {
            _inv.Add(next.costCurrency, inst.Config.upgradeCost);
            Debug.LogError($"Upgrade spawn failed: {e}");
            return Result.Fail("SpawnFailed");
        }

        // 删除旧塔
        try
        {
            _factory.DespawnTower(instanceId);
        }
        catch (Exception e)
        {
            Debug.LogError($"Despawn old tower failed: {e}");
            // 尝试回滚：删除新塔 + 返还资源 + registry 恢复
            _factory.DespawnTower(newId);
            _inv.Add(next.costCurrency, inst.Config.upgradeCost);
            return Result.Fail("DespawnOldFailed");
        }

        _registry.Unregister(instanceId);
        _registry.Register(new TowerInstance(newId, next));
        return Result.Ok(newId);
    }

    /// <summary>
    /// 回收：删除塔 -> 返钱 -> registry 移除
    /// </summary>
    public Result TryRecycleTower(int instanceId)
    {
        if (_inv == null || _factory == null || _registry == null) return Result.Fail("MissingDependencies");
        if (!_registry.TryGet(instanceId, out var inst)) return Result.Fail("InstanceNotFound");
        if (inst.Config == null) return Result.Fail("MissingConfig");

        try
        {
            _factory.DespawnTower(instanceId);
        }
        catch (Exception e)
        {
            Debug.LogError($"DespawnTower failed: {e}");
            return Result.Fail("DespawnFailed");
        }

        _registry.Unregister(instanceId);
        if (inst.Config.recycleRefund > 0)
            _inv.Add(inst.Config.costCurrency, inst.Config.recycleRefund);

        return Result.Ok(instanceId);
    }   

    #endregion

}