
using System;
using Pathfinding;
using UnityEngine;

public class GotAStarLinker : IGotLinker, PoolItem<object>
{
    public static DataObjPool<GotAStarLinker, object> pool =
        new DataObjPool<GotAStarLinker, object>("GotAStarLinker", 50);

    private PathFindingLinkerConfig _config;
    private bool isInPool = true;
    private bool isInit = false;
    private GameObject start;
    private GameObject end;
    private NodeLink2 nodeLink;

    public void Init(PathFindingLinkerConfig config)
    {
        if (isInit)
        {
            Debug.LogError("[GotPathFinding]Linker Init has been init");
            return;
        }
        
        _config = config;
        start = new GameObject("start");
        end = new GameObject("end");
        nodeLink = start.AddComponent<NodeLink2>();
        nodeLink.end = end.transform;

        SetGraph(_config.graphList);
        SetStartAndEndPos(_config.startPos, _config.endPos, _config.isSingleTrack);
        SetEnable(_config.isEnable);
        isInit = true;
    }

    public void SetEnable(bool enable)
    {
        if (!CheckExist())
        {
            return;
        }
        nodeLink.enabled = enable;
    }

    public void SetStartAndEndPos(Vector3 startPos, Vector3 endPos, bool isSingleTrack)
    {
        if (!CheckExist())
        {
            return;
        }
        start.transform.position = startPos;
        end.transform.position = endPos;
        nodeLink.oneWay = isSingleTrack;
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

        nodeLink.graphMask = mask;
    }

    public void Remove()
    {
        if (!CheckExist())
        {
            return;
        }

        if (!isInit)
        {
            Debug.LogError("[GotPathFinding]Linker Init has not been init,but you remove it.");
            return;
        }
        GameObject.Destroy(start);
        GameObject.Destroy(end);
        nodeLink = null;
        isInit = false;
        pool.RecoverItem(this);
    }

    private bool CheckExist()
    {
        if (isInPool)
        {
            Debug.LogError("[GotPathFinding]Linker has been recycled by the object pool,you cant use any function.");
#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Linker has been recycled by the object pool,you cant use any function.");
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