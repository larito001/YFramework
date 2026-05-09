using System.Collections.Generic;
using Pathfinding.RVO;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

[HelpURL("https://arongranberg.com/astar/documentation/stable/changelog.html")]
public class YAStarManager : IYPathFindingManager
{
    public const string fullPath = "Assets/Resources/Config/Astar/";
    public const string keyPointPath = "Assets/Script/Editor/PathFinding/linePoints";
    public const string basePath = "Config/Astar/";

    private readonly SceneReferenceService sceneReferenceService;
    private readonly ResMgr resMgr;

    public YAStarManager(SceneReferenceService referenceService, ResMgr resourceManager)
    {
        sceneReferenceService = referenceService;
        resMgr = resourceManager;
    }

    public void Unload()
    {
    }

    private UnityAction loadCompeleteCallBack;
    private GameObject astarPathObj;
    private readonly Dictionary<long, YAStarSeeker> aiDic = new Dictionary<long, YAStarSeeker>();
    private readonly Queue<long> removeQueue = new Queue<long>();
    private readonly Queue<YAStarSeeker> addQueue = new Queue<YAStarSeeker>();
    private int graphCount;
    private int graphCompleteCount;

    public void LoadPathFinding(UnityAction callback, string[] graphList)
    {
        graphCount = graphList.Length;
        loadCompeleteCallBack = callback;

        if (sceneReferenceService.TryGetTransform(SceneRefKeys.AStarRoot, out var astarTransform))
        {
            astarPathObj = astarTransform.gameObject;
        }

        AstarPath astarPath;
        if (astarPathObj == null)
        {
            astarPathObj = new GameObject(SceneRefKeys.AStarRoot);
            astarPath = astarPathObj.AddComponent<AstarPath>();
        }
        else
        {
            astarPath = astarPathObj.GetComponent<AstarPath>();
        }

        astarPath.logPathResults = Pathfinding.PathLog.None;
        ClearGraph();
        for (var i = 0; i < graphList.Length; i++)
        {
            AddGraph(basePath + graphList[i], OnAddGraphComplete);
        }

        astarPathObj.AddComponent<RVOSimulator>();
    }

    public void Update()
    {
        while (addQueue.Count > 0)
        {
            var seeker = addQueue.Dequeue();
            aiDic.Add(seeker.id, seeker);
        }

        while (removeQueue.Count > 0)
        {
            var id = removeQueue.Dequeue();
            aiDic.Remove(id);
        }

        foreach (var ai in aiDic.Values)
        {
            ai.Update();
        }
    }

    public void UnloadPathFinding()
    {
        ClearGraph();
        aiDic.Clear();
        GameObject.Destroy(astarPathObj);
    }

    public YAStarSeeker CreateSeeker(ICoroutineRunner runner)
    {
        var seeker = YAStarSeeker.pool.GetItem(null);
        seeker.ConfigureRuntime(runner, this);
        return seeker;
    }

    public YAStarObstacle CreateObstacle()
    {
        return YAStarObstacle.pool.GetItem(null);
    }

    public YAStarLinker CreateLinker()
    {
        return YAStarLinker.pool.GetItem(null);
    }

    public void SetGraph(string path, UnityAction callback)
    {
        ClearGraph();
        AddGraph(path, callback);
    }

    public void AddGraph(string path, UnityAction callback)
    {
        resMgr.LoadHandleAsync<TextAsset>(path, handle =>
        {
            var textAsset = handle?.Asset;
            if (textAsset == null)
            {
                callback?.Invoke();
                return;
            }

            try
            {
                AstarPath.active.data.DeserializeGraphsAdditive(textAsset.bytes);
                if (AstarPath.active.data.pointGraph != null)
                {
                    AstarPath.active.data.pointGraph.maxDistance = 10;
                }

                callback?.Invoke();
            }
            finally
            {
                handle.Release();
            }
        });
    }

    public void ClearGraph()
    {
        AstarPath.active?.data?.ClearGraphs();
    }

    public void AddAISearch(YAStarSeeker ai)
    {
        if (_addQueueContains(ai))
        {
            return;
        }

        if (!aiDic.ContainsKey(ai.id))
        {
            addQueue.Enqueue(ai);
        }
    }

    public void RemoveAISearch(YAStarSeeker ai)
    {
        if (removeQueue.Contains(ai.id))
        {
            return;
        }

        if (aiDic.ContainsKey(ai.id))
        {
            removeQueue.Enqueue(ai.id);
        }
    }

    private void OnAddGraphComplete()
    {
        graphCompleteCount++;
        if (graphCount <= graphCompleteCount)
        {
            loadCompeleteCallBack?.Invoke();
        }
    }

    private bool _addQueueContains(YAStarSeeker ai)
    {
        foreach (var item in addQueue)
        {
            if (item == ai)
            {
                return true;
            }
        }

        return false;
    }
}
