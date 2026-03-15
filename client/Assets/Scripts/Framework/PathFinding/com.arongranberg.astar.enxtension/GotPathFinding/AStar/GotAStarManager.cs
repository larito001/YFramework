using System.Collections.Generic;
using Pathfinding.RVO;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

[HelpURL("https://arongranberg.com/astar/documentation/stable/changelog.html")]
public class GotAStarManager : IGotPathFindingManager
{
    public const string fullPath = "Assets/Resources/Config/Astar/";
    public const string keyPointPath = "Assets/Script/Editor/PathFinding/linePoints";
    public const string basePath = "Config/Astar/";

    private static SceneReferenceService sceneReferenceService;
    private static ResMgr resMgr;

    public static void Configure(SceneReferenceService referenceService, ResMgr resourceManager)
    {
        sceneReferenceService = referenceService;
        resMgr = resourceManager;
    }

    public static void Unload()
    {
    }

    private UnityAction loadCompeleteCallBack;
    private GameObject astarPathObj;
    private readonly Dictionary<long, GotAStarSeeker> aiDic = new Dictionary<long, GotAStarSeeker>();
    private readonly Queue<long> removeQueue = new Queue<long>();
    private readonly Queue<GotAStarSeeker> addQueue = new Queue<GotAStarSeeker>();
    private int graphCount;
    private int graphCompleteCount;

    public void LoadPathFinding(UnityAction callback, string[] graphList)
    {
        graphCount = graphList.Length;
        PathFindingFactory.Init(PathFindingType.AStar);
        loadCompeleteCallBack = callback;

        if (sceneReferenceService.TryGetTransform(SceneReferenceKeys.AStarRoot, out var astarTransform))
        {
            astarPathObj = astarTransform.gameObject;
        }

        AstarPath astarPath;
        if (astarPathObj == null)
        {
            astarPathObj = new GameObject(SceneReferenceKeys.AStarRoot);
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
        PathFindingFactory.Uload();
        GameObject.Destroy(astarPathObj);
    }

    public void SetGraph(string path, UnityAction callback)
    {
        ClearGraph();
        AddGraph(path, callback);
    }

    public void AddGraph(string path, UnityAction callback)
    {
        resMgr.LoadBytes(path, textAsset =>
        {
            AstarPath.active.data.DeserializeGraphsAdditive(textAsset.bytes);
            if (AstarPath.active.data.pointGraph != null)
            {
                AstarPath.active.data.pointGraph.maxDistance = 10;
            }

            callback?.Invoke();
        });
    }

    public void ClearGraph()
    {
        AstarPath.active?.data?.ClearGraphs();
    }

    public void AddAISearch(GotAStarSeeker ai)
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

    public void RemoveAISearch(GotAStarSeeker ai)
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

    private bool _addQueueContains(GotAStarSeeker ai)
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
