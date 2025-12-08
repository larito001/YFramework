using System.Collections.Generic;
using UnityEngine;

public class PathFindingFactory
{
    private static bool isInit = false;
    private static PathFindingType pFType;

    public static void Init(PathFindingType pathFindingType)
    {
        if (isInit)
        {
            Debug.LogError("多次初始化PathFindingFactory");
            return;
        }
        pFType = pathFindingType;
        isInit = true;
    }

    public static void Uload()
    {
        if (!isInit)
        {
            Debug.LogError("多次销毁PathFindingFactory");
            return;
        }
        isInit = false;
    }

    public static IGotSeeker GetSeeker()
    {
        if (!isInit)
        {
            Debug.LogError("请初始化PathFindingFactory");
            return null;
        }


        if (pFType == PathFindingType.AStar)
        {
            return GotAStarSeeker.pool.GetItem(null);
        }

        Debug.LogError("PathFindingError，未找到寻路模块");
        return null;
    }

    public static IGotObstacle GetObstacle()
    {
        if (!isInit)
        {
            Debug.LogError("请初始化PathFindingFactory");
            return null;
        }

        if (pFType == PathFindingType.AStar)
        {
            return  GotAStartObstacle.pool.GetItem(null);
        }

        Debug.LogError("PathFindingError，未找到寻路模块");
        return null;
    }
    
    public static IGotLinker GetLinker()
    {
        if (!isInit)
        {
            Debug.LogError("请初始化PathFindingFactory");
            return null;
        }

        if (pFType == PathFindingType.AStar)
        {
            return GotAStarLinker.pool.GetItem(null);
        }

        Debug.LogError("PathFindingError，未找到寻路模块");
        return null;
    }
}