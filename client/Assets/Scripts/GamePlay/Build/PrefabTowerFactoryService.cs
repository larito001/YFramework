using System.Collections.Generic;
using UnityEngine;

public class PrefabTowerFactoryService : IGameService, ITowerFactory
{
    private readonly Dictionary<int, GameObject> _spawned = new Dictionary<int, GameObject>();
    private int _nextId = 1;

    public int SpawnTower(TowerConfigSO config, Vector3 position, Quaternion rotation)
    {
        if (config == null || config.prefab == null)
        {
            throw new System.InvalidOperationException("TowerConfigSO or prefab is missing.");
        }

        var instance = Object.Instantiate(config.prefab, position, rotation);
        var instanceId = _nextId++;
        _spawned[instanceId] = instance;
        return instanceId;
    }

    public void DespawnTower(int instanceId)
    {
        if (!_spawned.TryGetValue(instanceId, out var instance))
        {
            return;
        }

        _spawned.Remove(instanceId);
        if (instance != null)
        {
            Object.Destroy(instance);
        }
    }

    public void SetTowerPose(int instanceId, Vector3 position, Quaternion rotation)
    {
        if (!_spawned.TryGetValue(instanceId, out var instance) || instance == null)
        {
            return;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
    }

    public void Init(GameContext ctx)
    {
        _spawned.Clear();
        _nextId = 1;
    }

    public void Shutdown()
    {
        foreach (var item in _spawned.Values)
        {
            if (item != null)
            {
                Object.Destroy(item);
            }
        }

        _spawned.Clear();
    }
}
