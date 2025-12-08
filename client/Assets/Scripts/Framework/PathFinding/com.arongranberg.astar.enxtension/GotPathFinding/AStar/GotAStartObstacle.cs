using System;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public class GotAStartObstacle : IGotObstacle, PoolItem<object>
{
    public static DataObjPool<GotAStartObstacle, object> pool =
        new DataObjPool<GotAStartObstacle, object>("GotAStartObstacle", 50);

    List<NavmeshCut> cutters = new List<NavmeshCut>();
    private PathFindingObstacleConfig _config = null;
    private GameObject obj = null;
    // private NavmeshCut cutter = null;
    private bool isInPool = true;
    private bool isInit = false;

    public void Init(PathFindingObstacleConfig config)
    {
        if (isInit)
        {
            Debug.LogError("[GotPathFinding]Obstacle Init has been init");
            return;
        }

        _config = config;

        if (_config == null)
        {
            Debug.LogError("[GotPathFinding]Obstacle Init Config is null");
            return;
        }

        obj = _config.obstacleObject;
        if (obj == null)
        {
            Debug.LogError("[GotPathFinding]Obstacle Init obj is null ");
            return;
        }
        //todo:获取obj和其子对象的所有NavmeshCut组件
        cutters.Clear();
        obj.GetComponentsInChildren<NavmeshCut>(cutters);
        if (cutters.Count == 0)
        {
            Debug.LogError("[GotPathFinding]Obstacle Init obj has no NavmeshCut component");
            return;
        }

        foreach (var cutter in cutters)
        {
            cutter.useRotationAndScale = _config.useRotationAndScale;
            cutter.isDual = _config.isDual;
            cutter.cutsAddedGeom = false;
            cutter.updateDistance = _config.alwaysUpdate ? 0.01f : 9999f;
            cutter.updateRotationDistance = _config.useRotationAndScale ? 5f : 360f;
        };
        SetEnable(_config.isEnable);
        SetGraph(_config.graphList);

        
        if (!_config.isAddOnPrefab)
        {
            SetObstacleInfo(_config.info);
        }
        isInit = true;
    }

    public void SetGraph(string[] graphList)
    {
        if (!CheckExist())
        {
            return;
        }
        GraphMask mask = 0;
        for (var i = 0; i < graphList.Length; i++)
        {
            mask |= GraphMask.FromGraphName(graphList[i]);
        }

        if (graphList.Length == 0)
        {
            mask = GraphMask.everything;
        }

        foreach (var cutter in cutters)
        {
            cutter.graphMask = mask;
        }
 
    }

    public void SetEnable(bool enable)
    {
        if (!CheckExist())
        {
            return;
        }

        foreach (var cutter in cutters)
        {
            cutter.enabled = enable;
        }

    }

    public void Remove()
    {
        
        if (!isInit)
        {
            Debug.LogError("[GotPathFinding]Obstacle Init has not been init,but you remove it.");
            return;
        }
        if (isInPool)
        {
            Debug.LogError("[GotPathFinding]Obstacle has been recycled by the object pool,you cant use any function.");
#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Obstacle has been recycled by the object pool,you cant use any function.");
#endif
        }

        pool.RecoverItem(this);
        isInit = false;
        if (obj != null)
        {
            Debug.LogWarning("[GotPathFinding] obj is null before remove, is sure?");
            foreach (var cutter in cutters)
            {
                GameObject.Destroy(cutter);
            }
        }
        obj = null;
    }


    public void SetObstacleInfo(PathFindingObstacleConfig.GotObstacleShape info)
    {
        foreach (var cutter in cutters)
        {
            cutter.center = info.center;

            cutter.height = info.height; // Y方向高度

            if (info.shape == PathFindingObstacleConfig.GotObstacleType.Box)
            {
                cutter.type = NavmeshCut.MeshType.Box;
                cutter.rectangleSize = new Vector2(info.width, info.depth); // X, Y, Z方向的尺寸
            }
            else if (info.shape == PathFindingObstacleConfig.GotObstacleType.Polygon)
            {
                cutter.type = NavmeshCut.MeshType.Circle;
                cutter.circleResolution = Mathf.Clamp(info.resolution, 3, 12); // 多边形拟合精度
            }
            else
            {
                cutter.type = NavmeshCut.MeshType.Box;
                cutter.rectangleSize = new Vector2(info.width, info.depth); // X, Y, Z方向的尺寸
            }  
        }
    }

    private bool CheckExist()
    {
        if (isInPool)
        {
            Debug.LogError("[GotPathFinding]Obstacle has been recycled by the object pool,you cant use any function.");
#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Obstacle has been recycled by the object pool,you cant use any function.");
#endif
            return false;
        }

        if (obj == null)
        {
            Debug.LogError(
                "[GotPathFinding]Obstacle Object has already been destroyed but you are still trying to access it. This is invalid.");

#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Obstacle Object has already been destroyed but you are still trying to access it. This is invalid.");
#endif
            return false;
        }

        return true;
    }

    public void AfterIntoObjectPool()
    {
        isInPool = true;
    }

    public void SetData(object serverData)
    {
        isInPool = false;
    }
}