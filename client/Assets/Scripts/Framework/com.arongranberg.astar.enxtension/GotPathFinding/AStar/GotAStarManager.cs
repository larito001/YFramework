
using System.Collections.Generic;

using Pathfinding.RVO;
using UnityEngine;
using UnityEngine.Events;
using YOTO;


[HelpURL("https://arongranberg.com/astar/documentation/stable/changelog.html")]
public class GotAStarManager : IGotPathFindingManager
{

    #region 路径配置

    //editor保存路径
    public const string fullPath ="Assets/ResourcesAssets/Config/Astar/" ;
    public const string keyPointPath ="Assets/Script/Editor/PathFinding/linePoints";
    //manager加载路径
    public const string basePath = "Config/Astar/";
    //graph加载前配置路径
    public const string RoadPoint = "Road";
    public const string RaycastMesh = "Road2";
    public const string TowerDefenseRaycastMesh = "CityBigMesh";
    #endregion
    
    #region 单例

    private static GotAStarManager _instance;

    public static GotAStarManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GotAStarManager();
            }

            return _instance;
        }
    }

    public static void Unload()
    {
        _instance = null;
    }

    #endregion
    
    #region manager内部属性
    
    private UnityAction _loadCompeleteCallBack;//
    GameObject astarPathObj;
    private Dictionary<long, GotAStarSeeker> _aiDic = new Dictionary<long, GotAStarSeeker>();
    private Queue<long> _removeQueue = new Queue<long>();
    private Queue<GotAStarSeeker> _addQueue = new Queue<GotAStarSeeker>();
    private int graphCount =0;
    private int graphCompleteCount = 0;

    #endregion

    #region 生命周期，跟场景生命周期走

    public void LoadPathFinding(UnityAction loadCompeleteCallBack,string [] graphList)
    {
        graphCount=graphList.Length;
        PathFindingFactory.Init(PathFindingType.AStar);
        _loadCompeleteCallBack = loadCompeleteCallBack;
        astarPathObj = new GameObject("A*");
        var astarPath = astarPathObj.AddComponent<AstarPath>();
        astarPath.logPathResults = Pathfinding.PathLog.None;
        ClearGraph();
        for (var i = 0; i < graphList.Length; i++)
        {
            AddGraph(basePath+graphList[i],OnAddGraphComplete);
        }
        astarPathObj.AddComponent<RVOSimulator>();
        ////test：seeker的使用
        //var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //var seeker = PathFindingFactory.GetSeeker();
        //seeker.Init(new PathFindingSeekerConfig()
        //{
        //    moveObject = obj,
        //    quality = PathFindingSeekerConfig.PathFindingQuality.Heigh,
        //    graphList = new string[] { "Test2" },
        //});
        //GameObject.Destroy(obj.GetComponent<BoxCollider>());
        //obj.transform.position = new Vector3(82, 3.6f, 60);
        //obj.AddComponent<PlayerMovement>();
        //seeker.SetGraph(new string[] { "Test1" });
        ////test2:障碍的使用
        //var obj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //var ob = PathFindingFactory.GetObstacle();
        //ob.Init(new PathFindingObstacleConfig()
        //{
        //    obstacleObject = obj2,
        //});
        //ob.SetGraph(new string[] { "Test1" });
        //obj2.transform.position = new Vector3(82, 3.6f, 65);
    }

    private void OnAddGraphComplete()
    {
        graphCompleteCount++;
        if (graphCount <= graphCompleteCount)
        {
            _loadCompeleteCallBack?.Invoke();
        }
    }

    public void Update()
    {
        while (_addQueue.Count > 0)
        {
            var seeker = _addQueue.Dequeue();
            _aiDic.Add(seeker.id, seeker);
        }

        while (_removeQueue.Count > 0)
        {
            var id = _removeQueue.Dequeue();
            _aiDic.Remove(id);
        }

        foreach (var ai in _aiDic.Values)
        {
            ai.Update();
        }
    }

    public void UnloadPathFinding()
    {
        ClearGraph();
        // var data = AstarPath.active.data;
        // var myGraph = data.gridGraph;
        // data.RemoveGraph(myGraph);
        _aiDic.Clear();
        PathFindingFactory.Uload();
        GameObject.Destroy(astarPathObj);
    }

  

    #endregion

    #region 外部更改Graph的接口

    /// <summary>
    /// 设置（覆盖）图
    /// </summary>
    /// <param name="path"></param>
    public void SetGraph(string path,UnityAction callback)
    {
   
        // ResourceFunc.LoadResSync<TextAsset>(this, ResourceFunc.RES_EXT_TYPE.Bytes, path,
        //     (loadContent, reqID, loadlifeCycle) =>
        //     {
        //         var textAsset = loadContent as TextAsset;
        //         AstarPath.active.data.DeserializeGraphs(textAsset.bytes);
        //         if (AstarPath.active.data.pointGraph != null)
        //         {
        //             AstarPath.active.data.pointGraph.maxDistance = 10;
        //         }
        //
        //         LoadCompelete();
        //         Debug.Log(path + "[GotPathFinding] Graph load success");
        //         ResourceFunc.RemoveLifeRef(this, path);
        //     });
        ClearGraph();
        AddGraph(path, callback);
    }

    /// <summary>
    /// 添加图
    /// </summary>
    /// <param name="path"></param>
    public void AddGraph(string path, UnityAction callback)
    {
        YFramework.resMgr.LoadBytes(path, (textAsset) =>
        {
            AstarPath.active.data.DeserializeGraphsAdditive(textAsset.bytes);
            if (AstarPath.active.data.pointGraph != null)
            {
                AstarPath.active.data.pointGraph.maxDistance = 10;
            }

            callback?.Invoke();
            Debug.Log(path + "[GotPathFinding] Graph load success");
        });
    }

    public void ClearGraph()
    {
        AstarPath.active.data.ClearGraphs();
    }
    
    #endregion

    #region 组件内部使用，控制其他组件生命周期

    public void AddAISearch(GotAStarSeeker ai)
    {
        if (_addQueue.Contains(ai))
        {
            Debug.Log(ai.id + "[GotPathFinding] id has exist dont add repeatedly");
            return;
        }

        if (!_aiDic.ContainsKey(ai.id))
        {
            _addQueue.Enqueue(ai);
        }
        else
        {
            Debug.LogError("[GotPathFinding]" + ai.id + " id has exist dont add repeatedly");
        }
    }

    public void RemoveAISearch(GotAStarSeeker ai)
    {
        if (_removeQueue.Contains(ai.id))
        {
            Debug.LogError("[GotPathFinding] " + ai.id + " dont remove repeatedly");
            return;
        }

        if (!_aiDic.ContainsKey(ai.id))
        {
            Debug.LogError("[GotPathFinding] " + ai.id + " dont remove repeatedly");
        }
        else
        {
            _removeQueue.Enqueue(ai.id);
        }
    }

    #endregion
}