namespace Dreamteck.Splines.Examples
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Dreamteck.Splines;
    using System;

// 这个类用于控制火车头在样条曲线上的运动，并管理它与车厢之间的连接
    public class TrainEngine : MonoBehaviour
    {
        private SplineTracer _tracer = null; // 引用样条追踪器（可以是 SplineFollower）
        private double _lastPercent = 0.0; // 上一帧曲线百分比位置（用于计算切换点）
        private Wagon _wagon; // 引用当前挂载的车厢组件

        private void Awake()
        {
            follower = GetComponent<SplineFollower>();
            _wagon = GetComponent<Wagon>(); // 获取当前物体上的 Wagon 组件
        }

        void Start()
        {
            _tracer = GetComponent<SplineTracer>(); // 获取样条追踪器组件
            // 当经过一个节点（Node，可能是岔路口）时触发事件
            _tracer.onNode += OnJunction;
            // 当样条移动位置应用完毕时触发，用来更新车厢位置
            _tracer.onMotionApplied += OnMotionApplied;

            // 判断追踪器是否为 SplineFollower（样条跟随器）
            if (_tracer is SplineFollower)
            {
                SplineFollower follower = (SplineFollower)_tracer;
                Debug.Log("Subscribing to follower");
                // 当到达样条的起点时触发
                follower.onBeginningReached += FollowerOnBeginningReached;
                // 当到达样条的终点时触发
                follower.onEndReached += FollowerOnEndReached;
            }
        }

        private void OnMotionApplied()
        {
            // 当引擎位置更新后，更新车厢的偏移量（递归更新所有后续车厢）
            _lastPercent = _tracer.result.percent; // 保存当前位置的百分比
            _wagon.UpdateOffset(); // 通知车厢更新偏移
        }

        /// <summary>
        /// 当到达曲线起点并循环或反弹时，记录最后的百分比位置
        /// </summary>
        private void FollowerOnBeginningReached(double lastPercent)
        {
            _lastPercent = lastPercent; // 保存最后的位置百分比
        }

        /// <summary>
        /// 当到达曲线终点并循环或反弹时，记录最后的百分比位置
        /// </summary>
        private void FollowerOnEndReached(double lastPercent)
        {
            _lastPercent = lastPercent; // 保存最后的位置百分比
        }

        // 当样条追踪器经过一个结点（Node）时被调用
        private void OnJunction(List<SplineTracer.NodeConnection> passed)
        {
            Node node = passed[0].node; // 获取当前经过的结点（Node）
            JunctionSwitch junctionSwitch = node.GetComponent<JunctionSwitch>(); // 尝试在节点上查找一个 JunctionSwitch（转辙器开关）
            if (junctionSwitch == null) return; // 如果没有 JunctionSwitch，忽略该节点
            if (junctionSwitch.bridges.Length == 0) return; // 如果 JunctionSwitch 没有关联桥（连接），则忽略
            foreach (JunctionSwitch.Bridge bridge in junctionSwitch.bridges)
            {
                // 如果此桥未激活，则跳过
                if (!bridge.active) continue;
                if (bridge.a == bridge.b) continue; // 如果桥连接的是同一个样条，跳过
                int currentConnection = 0;
                // 获取当前节点的所有连接（样条入口/出口）
                Node.Connection[] connections = node.GetConnections();
                // 找到当前追踪器所在的样条连接索引
                for (int i = 0; i < connections.Length; i++)
                {
                    if (connections[i].spline == _tracer.spline)
                    {
                        currentConnection = i;
                        break;
                    }
                }

                // 如果当前连接不属于桥的两端，则跳过
                if (currentConnection != bridge.a && currentConnection != bridge.b) continue;
                // 如果当前在桥的一端 a 上
                if (currentConnection == bridge.a)
                {
                    // 检查行进方向是否匹配桥的方向
                    if ((int)_tracer.direction != (int)bridge.bDirection) continue;
                    // 找到了合适的桥，执行样条切换
                    SwitchSpline(connections[bridge.a], connections[bridge.b]);
                    return;
                }
                else
                {
                    // 如果当前在桥的另一端 b 上
                    if ((int)_tracer.direction != (int)bridge.aDirection) continue;
                    // 同样执行样条切换
                    SwitchSpline(connections[bridge.b], connections[bridge.a]);
                    return;
                }
            }
        }

        // 实际执行样条切换（即从一个样条段跳到另一个）
        void SwitchSpline(Node.Connection from, Node.Connection to)
        {
            // 计算上一个节点与当前位置之间的超出距离（单位长度）
            float excessDistance = from.spline.CalculateLength(from.spline.GetPointPercent(from.pointIndex), _tracer.UnclipPercent(_lastPercent));
            // 设置新的目标样条曲线（切换到新的轨道）
            _tracer.spline = to.spline;
            // 立即重建，更新内部数据
            _tracer.RebuildImmediate();
            // 计算新样条中该节点位置的百分比
            double startpercent = _tracer.ClipPercent(to.spline.GetPointPercent(to.pointIndex));
            // 判断两条样条的方向是否相反
            if (Vector3.Dot(from.spline.Evaluate(from.pointIndex).forward, to.spline.Evaluate(to.pointIndex).forward) < 0f)
            {
                // 如果方向相反，则翻转行进方向
                if (_tracer.direction == Spline.Direction.Forward) _tracer.direction = Spline.Direction.Backward;
                else _tracer.direction = Spline.Direction.Forward;
            }

            // 将追踪器设定到新的位置上，并沿新样条移动“超出距离”
            _tracer.SetPercent(_tracer.Travel(startpercent, excessDistance, _tracer.direction));
            // 通知车厢我们进入了新的样条段
            _wagon.EnterSplineSegment(from.pointIndex, _tracer.spline, to.pointIndex, _tracer.direction);
            // 更新车厢偏移（保持连接顺畅）
            _wagon.UpdateOffset();
        }
        [Header("Train Control Settings")]
        public float acceleration = 5f;  // 加速曲线（越大加速越猛）
        public float deceleration = 3f;  // 减速曲线
        public float maxSpeed = 20f;     // 最大速度（正反通用）

        private float currentSpeed = 0f; // 当前速度
        private SplineFollower follower;

        private void Update()
        {
            HandleInput();
        }

        void HandleInput()
        {
            float targetAcceleration = 0f;

            // 检测输入
            if (Input.GetKey(KeyCode.W))
            {
                // 前进加速
                targetAcceleration = acceleration;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                // 倒车加速
                targetAcceleration = -acceleration;
            }
            else
            {
                // 无按键时自动减速
                if (currentSpeed > 0f) targetAcceleration = -deceleration;
                else if (currentSpeed < 0f) targetAcceleration = deceleration;
                else targetAcceleration = 0f;
            }

            // 平滑速度变化
            currentSpeed += targetAcceleration * Time.deltaTime;

            // 限制最大最小速度
            currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed, maxSpeed);

            // 自动方向切换：正值为前进，负值为倒车
            if (Mathf.Abs(currentSpeed) > 0.05f)
            {
                follower.direction = currentSpeed >= 0f ? Spline.Direction.Forward : Spline.Direction.Backward;
            }

            // 设置 SplineFollower 的速度（确保为正的速度值）
            follower.followSpeed = currentSpeed;

            // 自动惰性减停
            if (Mathf.Abs(currentSpeed) < 0.05f && targetAcceleration == 0f)
                currentSpeed = 0f;
        }
        void LateUpdate()
        {
            if (_tracer == null || _tracer.spline == null) return;
        
            // 倒车时检测车尾是否靠近节点
            if (_tracer.direction == Spline.Direction.Backward)
            {
                Wagon tail = _wagon;
                while (tail.back != null) tail = tail.back;
        
                double tailPercent = tail.GetComponent<SplineTracer>().result.percent;
        
                // 检测是否接近终点或起点
                if (tailPercent < 0.01)
                {
                    // 尝试查找当前样条起点是否连接 Node
                    if (follower.followSpeed < 0)
                    {
                        follower.followSpeed = 0;
                    }
                }
            }
        }
        // private Node FindConnectedNode(SplineComputer spline, int pointIndex)
        // {
        //     Node[] allNodes = FindObjectsOfType<Node>();
        //     foreach (var node in allNodes)
        //     {
        //         var connections = node.GetConnections();
        //         foreach (var conn in connections)
        //         {
        //             if (conn.spline == spline && conn.pointIndex == pointIndex)
        //                 return node;
        //         }
        //     }
        //     return null;
        // }
    }
}
