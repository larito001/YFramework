using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum PathFindingType
{
    AStar,
}

public interface IGotPathFindingManager
{
    //加载寻路
    public void LoadPathFinding(UnityAction loadCompeleteCallBack, string[] graphList);

    //卸载寻路
    public void UnloadPathFinding();

    //设置图
    public void SetGraph(string path, UnityAction callback);

    public void AddGraph(string path, UnityAction callback);

    //卸载图
    public void ClearGraph();
}

[Flags]
public enum ModifierType
{
    None = 0,
    StartEndModifier = 1 << 0,
    FunnelModifier = 1 << 1,
    RadiusModifier = 1 << 2,
    RaycastModifier = 1 << 3,
    SimpleSmoothModifier = 1 << 4,
    FunnelModifierAndRadiusModifier = 6,
}

#region 配置类

public class AStarHighSeekerConfig : PathFindingSeekerConfig
{
    /// <summary>
    /// 是否启用重力。支持：high、mid
    /// </summary>
    public bool enableGravity = true;

    /// <summary>
    /// 碰撞盒子半径。支持：high、mid
    /// </summary>
    public float radio = 0.5f;

    /// <summary>
    /// 碰撞盒子高度。支持：high、mid
    /// </summary>
    public float height = 1.8f;

    /// <summary>
    /// 停止的距离。支持：high、mid
    /// </summary>
    public float stopDistance;

    /// <summary>
    /// 带所有参数的构造函数
    /// </summary>
    public AStarHighSeekerConfig(
        GameObject moveObject,
        bool isEnable = true,
        float speed = 10f,
        float rotateSpeed = 360f,
        bool isUpdate = false,
        bool useObstacleAvoidance = false,
        ModifierType modifierType = ModifierType.None,
        string[] graphList = null,
        UnityAction onPathComplete = null,
        UnityAction onMovingDontUseLambda = null,
        bool enableGravity = false,
        float radio = 0.5f,
        float height = 1.8f,
        float stopDistance = 0f)
    {
        // 父类字段
        this.moveObject = moveObject;
        this.isEnable = isEnable;
        this.speed = speed;
        this.rotateSpeed = rotateSpeed;
        this.isUpdate = isUpdate;
        this.UseObstacleAvoidance = useObstacleAvoidance;
        this.modifierType = modifierType;
        this.graphList = graphList ?? new string[] { };
        this.OnPathComplete = onPathComplete;
        this.OnMovingDontUseLambda = onMovingDontUseLambda;

        // 子类字段
        this.enableGravity = enableGravity;
        this.radio = radio;
        this.height = height;
        this.stopDistance = stopDistance;
    }
}

public class AStarMidSeekerConfig : PathFindingSeekerConfig
{
    /// <summary>
    /// 是否启用重力。支持：high、mid
    /// </summary>
    public bool enableGravity = false;

    /// <summary>
    /// 碰撞盒子半径。支持：high、mid
    /// </summary>
    public float radio = 0.5f;

    /// <summary>
    /// 碰撞盒子高度。支持：high、mid
    /// </summary>
    public float height = 1.8f;

    /// <summary>
    /// 停止的距离。支持：high、mid
    /// </summary>
    public float stopDistance;

    /// <summary>
    /// 加速度。支持：mid
    /// </summary>
    public float acceleration = 1000f;

    /// <summary>
    /// 减速距离。支持：mid
    /// </summary>
    public float slowDownDistance = 5f;

    /// <summary>
    /// 是否强制寻路在图内.支持：mid
    /// </summary>
    public bool constrainInsideGraph = false;

    /// <summary>
    /// 允许在角色圆形范围内超近道，设置太低会导致角色一卡一卡的，太高会穿墙。支持：mid
    /// </summary>
    public float pickNextWaypointDist = 0.3f;

    /// <summary>
    /// 带所有参数的构造函数
    /// </summary>
    public AStarMidSeekerConfig(
        GameObject moveObject,
        bool isEnable = true,
        float speed = 10f,
        float rotateSpeed = 360f,
        bool isUpdate = false,
        bool useObstacleAvoidance = false,
        ModifierType modifierType = ModifierType.None,
        string[] graphList = null,
        UnityAction onPathComplete = null,
        UnityAction onMovingDontUseLambda = null,
        bool enableGravity = true,
        float radio = 0.5f,
        float height = 1.8f,
        float stopDistance = 0f,
        float acceleration = 1000f,
        float slowDownDistance = 5f,
        bool constrainInsideGraph = false,
        float pickNextWaypointDist = 1
    )
    {
        // 父类字段
        this.moveObject = moveObject;
        this.isEnable = isEnable;
        this.speed = speed;
        this.rotateSpeed = rotateSpeed;
        this.isUpdate = isUpdate;
        this.UseObstacleAvoidance = useObstacleAvoidance;
        this.modifierType = modifierType;
        this.graphList = graphList ?? new string[] { };
        this.OnPathComplete = onPathComplete;
        this.OnMovingDontUseLambda = onMovingDontUseLambda;

        // 子类字段
        this.enableGravity = enableGravity;
        this.radio = radio;
        this.height = height;
        this.stopDistance = stopDistance;
        this.acceleration = acceleration;
        this.slowDownDistance = slowDownDistance;
        this.constrainInsideGraph = constrainInsideGraph;
        this.pickNextWaypointDist = pickNextWaypointDist;
    }
}

public class AStarLowSeekerConfig : PathFindingSeekerConfig
{

    /// <summary>
    /// 带所有参数的构造函数
    /// </summary>
    public AStarLowSeekerConfig(
        GameObject moveObject,
        bool isEnable = true,
        float speed = 10f,
        float rotateSpeed = 360f,
        bool isUpdate = false,
        bool useObstacleAvoidance = false,
        ModifierType modifierType = ModifierType.None,
        string[] graphList = null,
        UnityAction onPathComplete = null,
        UnityAction onMovingDontUseLambda = null
    )
    {
        // 父类字段
        this.moveObject = moveObject;
        this.isEnable = isEnable;
        this.speed = speed;
        this.rotateSpeed = rotateSpeed;
        this.isUpdate = isUpdate;
        this.UseObstacleAvoidance = useObstacleAvoidance;
        this.graphList = graphList ?? new string[] { };
        this.OnPathComplete = onPathComplete;
        this.OnMovingDontUseLambda = onMovingDontUseLambda;

        // 子类字段
        this.modifierType = modifierType;
    }
}

#endregion

public abstract class PathFindingSeekerConfig
{
    /// <summary>
    /// 需要寻路的对象
    /// </summary>
    public GameObject moveObject;

    /// <summary>
    /// 是否启用 。支持：high、mid、low
    /// </summary>
    public bool isEnable = true;

    /// <summary>
    /// 移动速度。支持：high、mid、low
    /// </summary>
    public float speed = 10f;

    /// <summary>
    /// 旋转速度。支持：high、mid、low
    /// </summary>
    public float rotateSpeed = 360f;

    /// <summary>
    /// 是否在update中调用寻路（打开后不再调用OnComplete，手动获取当前状态判断）。支持：high、mid、low
    /// </summary>
    public bool isUpdate = false;

    /// <summary>
    /// 是否启用避障。支持：high、mid、low
    /// </summary>
    public bool UseObstacleAvoidance = false;

    /// <summary>
    /// 寻路平滑。支持：mid、low
    /// </summary>
    public ModifierType modifierType = ModifierType.None;

    /// <summary>
    /// 网格索引,网格名称,默认允许所有网格上行走：Everything。支持：high、mid、low
    /// </summary>
    public string[] graphList = new string[] { };

    /// <summary>
    /// 移动完成后调用一次。【建议如果update中调用OncePathFinding，不使用OnPathComplete回调，使用GetIsMoving方法判断是否正在移动】。支持：high、mid、low
    /// </summary>
    public UnityAction OnPathComplete;

    /// <summary>
    /// 移动时调用，Update持续调用。支持：high、mid、low
    /// </summary>
    /// <remarks>
    /// 次函数严禁使用匿名，否则会产生大量内存垃圾
    /// </remarks>
    public UnityAction OnMovingDontUseLambda;
}

public interface IGotSeeker
{
    public void Init(PathFindingSeekerConfig config); //初始化
    public void SetEnable(bool enable); //是否启用，默认启用,归还控制权
    public void SetSpeed(float speed); //设置移动速度
    public void SetGraph(string[] graphList); //设置层级
    public void OncePathFinding(Vector3 pos, bool immediately = false); //一次寻路，不跟随目标
    public void TP(Vector3 pos);//传送
    public void AddForce(Vector3 dir, float force,UnityAction callback);//推，不影响转动
    
    public void StopPathFinding(); //尽可能停止寻路，配合ContinuePathFinding
    public void ContinuePathFinding(); //继续寻路，配合StopPathFinding
    public float GetDistance(); //获取距离目标点距离
    public Vector3 GetCurrentSpeed(); //获取当前速度

    public bool
        GetIsMoving(); //获取是否正在寻路,没到目标点就是false，无延迟，同步【建议如果update中调用OncePathFinding，不使用OnPathComplete回调，使用GetIsMoving方法判断是否正在移动】

    public void GetCanArrived(Vector3 pos, UnityAction<bool> callback); //检查是否能到达
    public void GetCanArrived(Transform trans, UnityAction<bool> callback); //检查是否能到达
    public List<Vector3> GetPath(); //获取路径
    public void Remove(); //移除
    public void SetUseObstacleAvoidance(bool useObstacleAvoidance); // 设置障碍开关
}

public class PathFindingObstacleConfig
{
    public enum GotObstacleType
    {
        /// <summary>
        /// 矩形
        /// </summary>
        Box,

        /// <summary>
        /// 多边形
        /// </summary>
        Polygon,
    }

    public class GotObstacleShape
    {
        /// <summary>
        /// 形状
        /// </summary>
        public GotObstacleType shape = GotObstacleType.Box;

        /// <summary>
        /// 通用：中心点
        /// </summary>
        public Vector3 center = Vector3.zero;
        
        /// <summary>
        /// 只适用于多边形：半径
        /// </summary>
        public float radius = 1;

        /// <summary>
        /// 通用：高度y
        /// </summary>
        public float height = 1;

        /// <summary>
        /// 只适用于矩形：深度z
        /// </summary>
        public float depth = 1;

        /// <summary>
        /// 只适用于矩形：宽度x
        /// </summary>
        public float width;

        /// <summary>
        /// 只适用于多边形：边数
        /// </summary>
        public int resolution = 1;
    }
    /// <summary>
    /// 是否提前加到了预制体上（如果提前把navcut加到预制体上调整，则宽高不生效）
    /// </summary>
    public bool isAddOnPrefab = false;
    /// <summary>
    /// 障碍物对象
    /// </summary>
    public GameObject obstacleObject;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool isEnable = true;

    /// <summary>
    /// 障碍物的配置信息
    /// </summary>
    public GotObstacleShape info = new GotObstacleShape();

    /// <summary>
    /// 是否为静态物体，静态物体会禁用旋转和缩放对cut的更改
    /// </summary>
    public bool useRotationAndScale = true;

    /// <summary>
    /// 是否持续更新，开启后建筑移动会触发更新
    /// </summary>
    public bool alwaysUpdate = false;

    /// <summary>
    /// 是否单纯对网格进行切割（只切割，不扣洞）
    /// </summary>
    public bool isDual = false;

    /// <summary>
    /// //影响的层级
    /// </summary>
    public string[] graphList = new string[] { };
    public PathFindingObstacleConfig(
        GameObject obstacleObject,
        bool isAddOnPrefab = false,
        bool isEnable = true,
        GotObstacleShape info = null,
        bool useRotationAndScale = true,
        bool alwaysUpdate = false,
        bool isDual = false,
        string[] graphList = null
    )
    {
        this.obstacleObject = obstacleObject ?? throw new ArgumentNullException(nameof(obstacleObject));
        this.isAddOnPrefab = isAddOnPrefab;
        this.isEnable = isEnable;
        this.info = info ?? new GotObstacleShape();
        this.useRotationAndScale = useRotationAndScale;
        this.alwaysUpdate = alwaysUpdate;
        this.isDual = isDual;
        this.graphList = graphList ?? new string[] { };
    }
}

public interface IGotObstacle
{
    public void Init(PathFindingObstacleConfig config); //初始化
    public void SetEnable(bool enable); //是否启用，默认启用
    public void Remove(); //移除障碍
    public void SetGraph(string[] graphList); //设置层级
    public void SetObstacleInfo(PathFindingObstacleConfig.GotObstacleShape info); //更改配置
}

public class PathFindingLinkerConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool isEnable = true;

    /// <summary>
    /// 起始的跳跃位置
    /// </summary>
    public Vector3 startPos;

    /// <summary>
    /// 结束的跳跃位置
    /// </summary>
    public Vector3 endPos;

    /// <summary>
    /// 是否单向行驶
    /// </summary>
    public bool isSingleTrack = false;

    /// <summary>
    /// //影响的层级
    /// </summary>
    public string[] graphList = new string[] { };
}

public interface IGotLinker
{
    public void Init(PathFindingLinkerConfig config); //初始化
    public void SetEnable(bool enable); //是否启用，默认启用
    public void SetStartAndEndPos(Vector3 startPos, Vector3 endPos, bool isSingleTrack); //修改起点和终点,是否单向
    public void SetGraph(string[] graphList); //设置层级
    public void Remove(); //移除linker
}