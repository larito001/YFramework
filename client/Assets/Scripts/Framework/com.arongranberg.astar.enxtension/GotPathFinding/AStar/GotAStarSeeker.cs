using System;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using Pathfinding.RVO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

[HelpURL("https://arongranberg.com/astar/documentation/stable/changelog.html")]
public class GotAStarSeeker : IGotSeeker, PoolItem<object>
{
    public static DataObjPool<GotAStarSeeker, object> pool =
        new DataObjPool<GotAStarSeeker, object>("GotAStarSeeker", 50);

    private static long staticId = 0;
    public long id = 0;
    private GameObject obj = null;
    private PathFindingSeekerConfig _config = null;
    private IAstarAI _aiEntity = null;
    private Seeker _seeker = null;
    private bool _isStarting = false; //是否启用OnComplete
    private bool isInit = false;
    private bool isInPool = true;
    private List<Vector3> pathBuffer = new List<Vector3>();
    private GraphMaskTraversalProvider _graphMaskTraversalProvider = null;
    private RVOController _rvoController = null;


    //尽量不要频繁调用
    private UnityAction<bool> GetCanArrivedCallback;

    SimpleSmoothModifier _simpleSmoothModifier = null;
    FunnelModifier _funnelModifier = null;
    RadiusModifier _radiusModifier = null;
    RaycastModifier _raycastModifier = null;


    // // 保存已经触发过的节点，防止重复触发
    // private HashSet<uint> reachedNodes = new HashSet<uint>();

    #region 配置

    private void HeighUseEntitiesConfig()
    {
#if MODULE_ENTITIES
            FollowerEntity fe = null;
            obj.TryGetComponent<FollowerEntity>(out fe);
            if (fe == null)
            {
                fe = obj.AddComponent<FollowerEntity>();
            }

            _aiEntity = fe;
            fe.enableGravity = _config.enableGravity;
            fe.radius = _config.radio;
            fe.height = _config.height;
            fe.maxSpeed = _config.speed;
            fe.rotationSpeed = _config.rotateSpeed;
            fe.stopDistance = _config.stopDistance;
            fe.enableLocalAvoidance = config.UseObstacleAvoidance;

            //这两行代码动了可能出问题
            fe.pathfindingSettings.graphMask = GraphMask.everything;
            fe.pathfindingSettings.traversalProvider = _graphMaskTraversalProvider;
#endif
#if !MODULE_ENTITIES
        throw new Exception("[GotPathFinding] Entities未安装，请勿使用此质量寻路");
#endif
    }

    private void MidConfig()
    {
        var config = _config as AStarMidSeekerConfig;
        if (config == null)
        {
            Debug.LogError("[GotPathFinding] Init Config is null");
            return;
        }

        _seeker = obj.AddComponent<Seeker>();
        _aiEntity = obj.AddComponent<AIPath>();
        var ap = _aiEntity as AIPath;
        if (!config.enableGravity)
        {
            ap.gravity = Vector3.zero;
        }
        else
        {
            ap.gravity = new Vector3(float.NaN, float.NaN, float.NaN); //默认使-9.8
            //todo:目前以所有layer都生效
            ap.groundMask = -1;
        }

        ap.pickNextWaypointDist = config.pickNextWaypointDist;
        ap.radius = config.radio;
        ap.height = config.height;
        ap.maxSpeed = config.speed;
        ap.rotationSpeed = config.rotateSpeed;
        ap.endReachedDistance = config.stopDistance;
        ap.slowdownDistance = config.slowDownDistance;
        ap.maxAcceleration = config.acceleration;
        ap.constrainInsideGraph = config.constrainInsideGraph; //让角色不穿模,路点不能开启
        _seeker.graphMask = GraphMask.everything;
        _seeker.traversalProvider = _graphMaskTraversalProvider;
        if (config.UseObstacleAvoidance)
        {
            _rvoController = obj.AddComponent<RVOController>();
            _rvoController.agentTimeHorizon = 3f;
            _rvoController.obstacleTimeHorizon = 3f;
        }

        ModifierConfig();
    }

    private void LowConfig()
    {
        var config = _config as AStarLowSeekerConfig;
        if (config == null)
        {
            Debug.LogError("[GotPathFinding] Init Config is null");
            return;
        }

        _seeker = obj.AddComponent<Seeker>();
        _aiEntity = obj.AddComponent<AILerp>();
        var al = _aiEntity as AILerp;
        // al.radius = _config.radio;不支持
        // al.height = _config.height;不支持
        al.speed = config.speed;
        // al.stopDistance = _config.stopDistance;不支持
        al.rotationSpeed = config.rotateSpeed;
        _seeker.graphMask = GraphMask.everything;
        _seeker.traversalProvider = _graphMaskTraversalProvider;
        if (config.UseObstacleAvoidance)
        {
            _rvoController = obj.AddComponent<RVOController>();
        }

        ModifierConfig();
    }

    #region 平滑配置

    private void ModifierConfig()
    {
        var types = _config.modifierType;
        // 遍历所有枚举值
        foreach (ModifierType type in Enum.GetValues(
                     typeof(ModifierType)))
        {
            if (type == ModifierType.None) continue; // 跳过 None

            if ((types & type) == type) // 判断是否包含
            {
                switch (type)
                {
                    case ModifierType.StartEndModifier:
                    {
                        StartEndModifierConfig();
                        break;
                    }
                    case ModifierType.FunnelModifier:
                    {
                        FunnelModifierConfig();
                        break;
                    }
                    case ModifierType.RadiusModifier:
                    {
                        RadiusModifierConfig();
                        break;
                    }
                    case ModifierType.RaycastModifier:
                    {
                        RaycastModifierConfig();
                        break;
                    }
                    case ModifierType.SimpleSmoothModifier:
                    {
                        SimpleSmoothModifierConfig();
                        break;
                    }
                }
                // 在这里做对应的逻辑处理
            }
        }
    }

    private void SimpleSmoothModifierConfig(bool enable = true)
    {
        _simpleSmoothModifier = _seeker.AddComponent<SimpleSmoothModifier>();
        // //平滑方式：
        // {
        //     //简单lerp平滑，容易在拐角出现截角
        //     _simpleSmoothModifier.smoothType = SimpleSmoothModifier.SmoothType.Simple;
        //     //配置
        //     _simpleSmoothModifier.uniformLength = true;
        //     _simpleSmoothModifier.maxSegmentLength = 2;
        //     _simpleSmoothModifier.iterations = 2;
        //     _simpleSmoothModifier.strength = 0.5f;
        // }
        {
            //贝塞尔曲线平滑
            _simpleSmoothModifier.smoothType = SimpleSmoothModifier.SmoothType.Bezier;
            //配置
            _simpleSmoothModifier.subdivisions = 2;
            _simpleSmoothModifier.bezierTangentLength = 0.4f;
        }

        _seeker.RegisterModifier(_simpleSmoothModifier);
    }

    private void RaycastModifierConfig(bool enable = true)
    {
        _raycastModifier = _seeker.AddComponent<RaycastModifier>();
        _raycastModifier.useRaycasting = false;
        _raycastModifier.useGraphRaycasting = true;
    }

    private void RadiusModifierConfig(bool enable = true)
    {
        _radiusModifier = _seeker.AddComponent<RadiusModifier>();
        _radiusModifier.radius = 1;
        _radiusModifier.detail = 10;
        _seeker.RegisterModifier(_radiusModifier);
    }

    private void FunnelModifierConfig(bool enable = true)
    {
        _funnelModifier = _seeker.AddComponent<FunnelModifier>();
        _funnelModifier.quality = FunnelModifier.FunnelQuality.Medium;
        _funnelModifier.splitAtEveryPortal = false;
        _funnelModifier.accountForGridPenalties = false;
        _seeker.RegisterModifier(_funnelModifier);
    }

    private void StartEndModifierConfig(bool enable = true)
    {
        //seeker,自带
        _seeker.startEndModifier.exactStartPoint = StartEndModifier.Exactness.NodeConnection;
        _seeker.startEndModifier.exactEndPoint = StartEndModifier.Exactness.NodeConnection;
        _seeker.startEndModifier.addPoints = true;
    }

    #endregion

    #endregion

    #region 生命周期

    public void AfterIntoObjectPool()
    {
        isInPool = true;
    }

    public void SetData(object serverData)
    {
        isInPool = false;
    }

    public void Init(PathFindingSeekerConfig config = null)
    {
        id = staticId++;


        if (config == null)
        {
            Debug.LogError("[GotPathFinding]Seeker Init Config is null");
            return;
        }

        obj = config.moveObject;
        if (obj == null)
        {
            Debug.LogError("[GotPathFinding]Seeker Init obj is null ");
            return;
        }

        _config = config;
        if (!CheckExist())
        {
            return;
        }


        _graphMaskTraversalProvider = new GraphMaskTraversalProvider(_config.graphList);

        if (config is AStarHighSeekerConfig)
        {
            HeighUseEntitiesConfig();
        }
        else if (config is AStarMidSeekerConfig)
        {
            MidConfig();
        }
        else if (config is AStarLowSeekerConfig)
        {
            LowConfig();
        }

        SetEnable(config.isEnable);
        GotAStarManager.Instance.AddAISearch(this);
        isInit = true;
    }

    public void Update()
    {
        if (!isInit) return;
        if (!CheckExist())
        {
            return;
        }

        // var nodes = _seeker.GetCurrentPath().path;
        // for (int i = 0; i < nodes.Count; i++)
        // {
        //     // 判断是否到达节点
        //     Vector3 nodePos = (Vector3)nodes[i].position;
        //     uint nodeIndex = nodes[i].NodeIndex;
        //     // 已经触发过的节点跳过
        //     if (reachedNodes.Contains(nodeIndex)) continue;
        //     if (Vector3.Distance(obj.transform.position, nodePos) <= 2)
        //     {
        //         // 触发事件
        //         Debug.Log("到达节点 / 节点位置: " + nodePos);
        //         // 记录已触发
        //         reachedNodes.Add(nodeIndex);
        //
        //
        //         // 可在这里调用节点事件，比如：
        //         // node.DoSomething();
        //     }
        // }

        if (_isStarting && !_config.isUpdate)
        {
            _config.OnMovingDontUseLambda?.Invoke();
        }

        if (_isStarting && !this.GetIsMoving() && !_config.isUpdate)
        {
            PathFindingEnd();
        }
    }

    public void Remove()
    {
        if (!isInit || isInPool)
        {
            Debug.LogError("[GotPathFinding]Seeker has been recycled by the object pool,you cant remove it again.");
#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Seeker has been recycled by the object pool,you cant remove it again.");
#endif
            return;
        }

        if (_config is AStarHighSeekerConfig)
        {
#if MODULE_ENTITIES
            GameObject.Destroy(_aiEntity as FollowerEntity);
#endif
        }
        else if (_config is AStarMidSeekerConfig)
        {
            GameObject.Destroy(_aiEntity as AIPath);

            if (_config.UseObstacleAvoidance)
            {
                GameObject.Destroy(_rvoController);
            }

            GameObject.Destroy(obj.GetComponent<Seeker>());
        }
        else if (_config is AStarLowSeekerConfig)
        {
            GameObject.Destroy(_aiEntity as AILerp);
            if (_config.UseObstacleAvoidance)
            {
                GameObject.Destroy(_rvoController);
            }

            GameObject.Destroy(obj.GetComponent<Seeker>());
        }

        isInit = false;
        obj = null;
        pool.RecoverItem(this);
        GotAStarManager.Instance.RemoveAISearch(this);
    }

    #endregion

    #region 寻路开始，结束方法

    public void SetEnable(bool enable)
    {
        if (!CheckExist())
        {
            return;
        }

        _aiEntity.canMove = enable;
    }

    public void OncePathFinding(Vector3 pos, bool immediately = false)
    {
        if (!CheckExist())
        {
            return;
        }

        SetEnable(true);
        _aiEntity.destination = pos;
        if (immediately)
        {
            _aiEntity.SearchPath();
        }

        _aiEntity.isStopped = false;
        _isStarting = true;
    }

    public void TP(Vector3 pos)
    {
        _aiEntity.Teleport(pos, true); //tp后切断寻路
    }

    private Coroutine currentMoveCoroutine;

    public void AddForce(Vector3 dir, float force,UnityAction callback)
    {
        // 如果正在移动，停止之前的移动
        if (currentMoveCoroutine != null)
        {
            YFramework.Instance.StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine=null;
        }

        // 计算目标位置
        Vector3 targetPosition = obj.transform.position + dir.normalized * force;

        // 开始安全的移动协程
        currentMoveCoroutine =     YFramework.Instance.StartCoroutine(SafeMoveToPosition(targetPosition, callback));
    }

    private IEnumerator SafeMoveToPosition( Vector3 delta, UnityAction callback)
    {
        Vector3 startPos = obj.transform.position;
        Vector3 targetPos = delta;
        if (AstarPath.active.Linecast(startPos, targetPos, out GraphHitInfo hitInfo))
        {
            //如果碰到障碍物了
            targetPos=hitInfo.point;
            // 往回偏移 0.5，避免紧贴障碍
            Vector3 dir = (targetPos - startPos).normalized;
            targetPos -= dir * 0.5f;
        }
        
        // 固定的移动时间
        float moveDuration = 0.5f;
        float t = 0f;

        // 内置的衰减曲线（EaseOut：开始快，慢慢停下）
        AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float lerpFactor = moveCurve.Evaluate(t / moveDuration);

            obj.transform.position = Vector3.Lerp(startPos, targetPos, lerpFactor);
            yield return null;
        }

        // 保证到达最终点
        obj.transform.position = targetPos;
        callback?.Invoke();
    }

    /// <summary>
    /// 判断两点之间是否可达
    /// </summary>
    private bool IsReachable(Vector3 from, Vector3 to)
    {
        var con = new NNConstraint();
        var startNode = AstarPath.active.GetNearest(from, con).node;
        var endNode = AstarPath.active.GetNearest(to, con).node;

        // 如果任一节点无效，就不可达
        if (startNode == null || endNode == null) return false;

        // 利用 PathUtilities 检查两点是否在同一连通区
        return PathUtilities.IsPathPossible(startNode, endNode);
    }
    public void StopPathFinding()
    {
        if (_isStarting && !_aiEntity.isStopped)
        {
            if (!CheckExist())
            {
                return;
            }

            _aiEntity.isStopped = true;
            _isStarting = false;
        }
        else
        {
            Debug.LogError("[GotPathFinding] PathFinding Stop Error,because is not starting!");
        }
    }

    public void ContinuePathFinding()
    {
        if (!_isStarting && _aiEntity.isStopped)
        {
            if (!CheckExist())
            {
                return;
            }

            _aiEntity.isStopped = false;
            _isStarting = true;
        }
        else
        {
            Debug.LogError("[GotPathFinding] PathFinding Stop Error,because is not starting!");
        }
    }

    private void PathFindingEnd()
    {
        if (!CheckExist())
        {
            return;
        }

        _isStarting = false;
        _config.OnPathComplete?.Invoke();
        SetEnable(false);
        // Debug.Log("[GotPathFinding] Path Finding End Success");
    }

    #endregion

    #region 实时设置参数（setter）

    public void SetSpeed(float speed)
    {
        if (!CheckExist())
        {
            return;
        }

        if (_config is AStarHighSeekerConfig)
        {
#if MODULE_ENTITIES
        var fe = _aiEntity as FollowerEntity;
            fe.maxSpeed = speed;
#endif
        }
        else if (_config is AStarMidSeekerConfig)
        {
            var ap = _aiEntity as AIPath;
            ap.maxSpeed = speed;
        }
        else if (_config is AStarLowSeekerConfig)
        {
            var al = _aiEntity as AILerp;
            al.speed = speed;
        }
    }

    public void SetGraph(string[] graphList)
    {
        _graphMaskTraversalProvider.ReSetAllowGraphs(graphList);
    }

    public void SetUseObstacleAvoidance(bool useObstacleAvoidance)
    {
        if (useObstacleAvoidance)
        {
            if (_rvoController == null)
            {
                _rvoController = obj.AddComponent<RVOController>();
            }
            _rvoController.enabled = true;
        }
        else
        {
            if (_rvoController != null)
            {
                _rvoController.enabled = false;
            }
        }
    }
    #endregion

    #region 获取当前状态（getter）

    public Vector3 GetCurrentSpeed()
    {
        if (!CheckExist())
        {
            return Vector3.zero;
        }

        return _aiEntity.velocity;
    }

    public bool GetIsMoving()
    {
        if (!CheckExist())
        {
            return false;
        }

        return !(_aiEntity.reachedEndOfPath && !_aiEntity.pathPending);
    }

    public void GetCanArrived(Vector3 pos, UnityAction<bool> callback)
    {
        if (!CheckExist())
        {
            return;
        }

        GetCanArrivedCallback = callback;
        ABPath.Construct(_aiEntity.position, pos, GetCanArrivedCallBack);
    }

    private void GetCanArrivedCallBack(Path path)
    {
        if (!CheckExist())
        {
            return;
        }

        GetCanArrivedCallback?.Invoke(!path.error);
    }

    public void GetCanArrived(Transform trans, UnityAction<bool> callback)
    {
        GetCanArrived(trans.position, callback);
    }

    public float GetDistance()
    {
        if (!CheckExist())
        {
            return 0;
        }

        return _aiEntity.remainingDistance;
    }

    public void FinalizeMovement(Vector3 nextPosition, Quaternion nextRotation)
    {
        _aiEntity.FinalizeMovement(nextPosition, nextRotation);
    }

    public List<Vector3> GetPath()
    {
        if (!CheckExist())
        {
            return null;
        }

        pathBuffer.Clear();
        _aiEntity.GetRemainingPath(pathBuffer, out bool stale);
        if (stale) //路径过时
        {
            pathBuffer.Clear();
        }

        return pathBuffer;
    }

    private bool CheckExist()
    {
        if (isInPool)
        {
            Debug.LogError("[GotPathFinding]Seeker has been recycled by the object pool,you cant use any function.");
#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Seeker has been recycled by the object pool,you cant use any function.");
#endif
            return false;
        }

        if (obj == null)
        {
            Debug.LogError(
                "[GotPathFinding]Seeker Object has already been destroyed but you are still trying to access it. This is invalid.");
#if UNITY_EDITOR
            throw new Exception(
                "[GotPathFinding]Seeker Object has already been destroyed but you are still trying to access it. This is invalid.");
#endif
            return false;
        }

        return true;
    }

    #endregion
}

/// <summary>
/// 自定义图筛选器
/// </summary>
public class GraphMaskTraversalProvider : ITraversalProvider
{
    private GraphMask allowedGraphs;

    public GraphMaskTraversalProvider(string[] allowedGraphNames)
    {
        ReSetAllowGraphs(allowedGraphNames);
    }

    public void ReSetAllowGraphs(string[] allowedGraphNames)
    {
        allowedGraphs = 0;
        foreach (var graphName in allowedGraphNames)
        {
            allowedGraphs |= GraphMask.FromGraphName(graphName);
        }

        if (allowedGraphNames.Length == 0)
        {
            allowedGraphs = GraphMask.everything;
        }
    }

    // 判断节点是否允许通行：只允许在 allowedGraphs 掩码范围内的图
    public bool CanTraverse(GraphNode node)
    {
        NavGraph graph = AstarPath.active.graphs[node.GraphIndex];
        GraphMask nodeMask = GraphMask.FromGraph(graph);
        return (allowedGraphs & nodeMask) != 0;
    }

    public bool CanTraverse(Path path, GraphNode node)
    {
        return CanTraverse(node);
    }
}