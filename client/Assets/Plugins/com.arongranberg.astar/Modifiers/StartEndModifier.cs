using System;
using UnityEngine;
using System.Collections.Generic;

namespace Pathfinding
{
    /// <summary>
    /// 调整路径的起点和终点。
    ///
    /// 该修改器包含在 <see cref="Pathfinding.Seeker"/> 组件中，如果你使用 Seeker，它总会被使用。
    /// 当路径被计算时，结果路径只包含它经过的节点的位置。
    /// 然而，很多时候你可能并不希望导航到某个特定节点的中心，而是希望到达节点表面上的某个点。
    /// 这个修改器会调整路径的起点和终点。
    ///
    /// [打开在线文档查看示意图]
    /// </summary>
    [System.Serializable]
    public class StartEndModifier : PathModifier
    {
        public override int Order
        {
            get { return 0; }
        }

        /// <summary>
        /// 向路径添加点，而不是替换它们。
        /// 例如，如果 <see cref="exactEndPoint"/> 设置为 ClosestOnNode，那么路径会被修改为
        /// 先到达路径中最后一个节点的中心点，然后再到达路径请求中的目标点所在节点的最近点。
        ///
        /// 但是，如果此值为 false，则路径中的相关点将直接被替换。
        /// 在上面的例子中，路径会直接到达节点上的最近点，而不会经过节点的中心。
        /// 也就是说如果addPoint为false， 角色会在到达最后一个网格时，直接前往目标点（假设目标点不在网格中心），为true就是先去网格中心再去目标点,默认为false
        /// </summary>
        public bool addPoints=false;

        /// <summary>
        /// 路径起点的确定方式。
        /// 参见: <see cref="Exactness"/>
        /// </summary>
        public Exactness exactStartPoint = Exactness.ClosestOnNode;

        /// <summary>
        /// 路径终点的确定方式。
        /// 参见: <see cref="Exactness"/>
        /// </summary>
        public Exactness exactEndPoint = Exactness.ClosestOnNode;

        /// <summary>
        /// 当路径被处理时调用。
        /// 返回的值将作为路径的起点，并可能根据 <see cref="exactStartPoint"/> 字段的值进行限制。
        /// 仅在 Original、Interpolate 和 NodeConnection 模式下使用。
        /// 动态修改路径的起点和终点，一般情况下，在角色不在单元格中心的时候，需要设置adjustStartPoint为当前角色位置，这样角色就不会掉头或者路径突变
        /// </summary>
        public System.Func<Vector3> adjustStartPoint;

        /// <summary>
        /// 设置路径的起点和终点应该放置在哪里。
        ///
        /// 下面是一个图例，展示了上述图片中不同元素的含义。
        /// 上述图片展示了一条路径从左上角进入，在靠近障碍物的节点结束，以及两种不同的可能终点位置，以及它们会如何被修改。
        /// [打开在线文档查看示意图]
        /// </summary>
        public enum Exactness
        {
            /// <summary>
            /// 点会被吸附到路径的第一个/最后一个节点的位置。
            /// 如果你的游戏是非常基于网格的，并且希望角色精确地停在节点的中心点，可以使用这个选项。
            /// 如果在角色移动过程中重新计算路径，你可能希望起点吸附使用 ClosestOnNode，
            /// 而终点吸附使用 SnapToNode，
            /// 因为在移动过程中角色通常不会正好位于节点中心。
            ///
            /// [打开在线文档查看示意图]
            /// </summary>
            SnapToNode,

            /// <summary>
            /// 点会被设置为创建路径请求时传入的精确位置。
            /// 需要注意的是，如果请求的目标点位于障碍物内部，
            /// 那么路径的最后一个点也会在障碍物内部，这通常不是你想要的结果。
            /// 建议改用 <see cref="Exactness.ClosestOnNode"/> 选项。
            ///
            /// [打开在线文档查看示意图]
            /// </summary>
            Original,
            /// <summary>
            /// 点会被设置为起点/终点前两个点连线上的最近点。
            /// 通常你会更想使用 NodeConnection 模式，因为那才是大多数情况下真正想要的行为。
            /// 该模式主要是为了兼容性原因而存在。
            /// [打开在线文档查看示意图]
            /// 已弃用：请使用 NodeConnection 替代。
            /// </summary>
            [Obsolete("已弃用：请使用 NodeConnection 替代")]
            Interpolate,

            /// <summary>
            /// 点会被设置为节点表面上的最近点。
            /// 注意：某些节点类型（例如点节点）没有表面，此时“最近点”就是节点位置，
            /// 这会使其等同于 <see cref="Exactness.SnapToNode"/>。
            /// 在自由移动的 3D 世界中，这几乎是你最常用的模式。
            /// [打开在线文档查看示意图]
            /// </summary>
            ClosestOnNode,

            /// <summary>
            /// 点会被设置为起点/终点节点的某个连接上的最近点。
            /// 在基于网格或点图的世界中，当使用 AILerp 脚本时，这个模式可能会很有用。
            ///
            /// 注意：如果你在 <see cref="Pathfinding.PointGraph"/> 中使用该模式，
            /// 可能还需要将 <see cref="PointGraph.nearestNodeDistanceMode"/> 设置为
            /// <see cref="Pathfinding.PointGraph.NodeDistanceMode.Connection"/>。
            ///
            /// [打开在线文档查看示意图]
            /// </summary>
            NodeConnection,
        }


        /// <summary>
        /// 从节点的中心到由 <see cref="Exactness"/> 确定的点进行直线检测。
        /// 在极少数情况下你会想使用它。它主要是为了向后兼容。
        ///
        /// 版本：自 4.1 起，该字段仅对 Original 模式有效，因为那是唯一合理的情况。
        /// </summary>
        [Obsolete("已弃用：默认为false")]
        public bool useRaycasting = false;

        public LayerMask mask = -1;

        /// <summary>
        /// 从节点的中心到由 <see cref="Exactness"/> 确定的点进行直线检测。
        /// 参见: <see cref="useRaycasting"/>
        ///
        /// 版本：自 4.1 起，该字段仅对 Original 模式有效，因为那是唯一合理的情况。
        /// </summary>
        public bool useGraphRaycasting = false;

        List<GraphNode> connectionBuffer;
        System.Action<GraphNode> connectionBufferAddDelegate;

        public override void Apply(Path _p)
        {
            var p = _p as ABPath;

            // 该修改器仅支持 ABPath（对其他路径没有意义）
            if (p == null || p.vectorPath.Count == 0) return;

            bool singleNode = false;

            if (p.vectorPath.Count == 1 && !addPoints)
            {
                // 复制第一个点
                p.vectorPath.Add(p.vectorPath[0]);
                singleNode = true;
            }

            // 添加而不是替换点
            bool forceAddStartPoint, forceAddEndPoint;
            // Which connection the start/end point was on (only used for the Connection mode)
            int closestStartConnection, closestEndConnection;

            Vector3 pStart = Snap(p, exactStartPoint, true, out forceAddStartPoint, out closestStartConnection);
            Vector3 pEnd = Snap(p, exactEndPoint, false, out forceAddEndPoint, out closestEndConnection);

            // 特殊情况：路径只有一个节点且使用 Connection 模式。
            // （forceAddStartPoint/forceAddEndPoint 仅在 Connection 模式下使用）
            // 在这种情况下，起点和终点位于节点的连接上。
            // 有两种情况：
            // 1. 如果起点和终点位于同一个连接上，我们不希望路径经过节点中心，
            // 而是直接从一个点到另一个点。
            // 这种情况是 closestStartConnection == closestEndConnection。
            // 2. 如果起点和终点位于不同的连接上，我们希望路径经过节点中心，
            // 从一个连接到另一个连接。
            // 但是无论哪种情况，我们都只希望节点中心被添加一次，
            // 因此我们始终将 forceAddStartPoint 设置为 false。
            if (singleNode)
            {
                if (closestStartConnection == closestEndConnection)
                {
                    forceAddStartPoint = false;
                    forceAddEndPoint = false;
                }
                else
                {
                    forceAddStartPoint = false;
                }
            }


            // 添加或替换起点
            // 如果模式是 SnapToNode，则禁用添加点
            // 因为此时 vectorPath 的第一个点很可能与第一个节点的位置相同
            if ((forceAddStartPoint || addPoints) && exactStartPoint != Exactness.SnapToNode)
            {
                p.vectorPath.Insert(0, pStart);
            }
            else
            {
                p.vectorPath[0] = pStart;
            }

            if ((forceAddEndPoint || addPoints) && exactEndPoint != Exactness.SnapToNode)
            {
                p.vectorPath.Add(pEnd);
            }
            else
            {
                p.vectorPath[p.vectorPath.Count - 1] = pEnd;
            }
        }

        Vector3 Snap(ABPath path, Exactness mode, bool start, out bool forceAddPoint, out int closestConnectionIndex)
        {
            var index = start ? 0 : path.path.Count - 1;
            var node = path.path[index];
            var nodePos = (Vector3)node.position;

            closestConnectionIndex = 0;

            forceAddPoint = false;

            switch (mode)
            {
                case Exactness.ClosestOnNode:
                    return start ? path.startPoint : path.endPoint;
                case Exactness.SnapToNode:
                    return nodePos;
                case Exactness.Original:
                case Exactness.Interpolate:
                case Exactness.NodeConnection:
                    Vector3 relevantPoint;
                    if (start)
                    {
                        relevantPoint = adjustStartPoint != null ? adjustStartPoint() : path.originalStartPoint;
                    }
                    else
                    {
                        relevantPoint = path.originalEndPoint;
                    }

                    switch (mode)
                    {
                        case Exactness.Original:
                            return GetClampedPoint(nodePos, relevantPoint, node);
                        case Exactness.Interpolate:
                            // 与起始节点或终止节点相邻的节点
                            var adjacentNode = path.path[Mathf.Clamp(index + (start ? 1 : -1), 0, path.path.Count - 1)];
                            return VectorMath.ClosestPointOnSegment(nodePos, (Vector3)adjacentNode.position,
                                relevantPoint);
                        case Exactness.NodeConnection:
                            // 这段代码使用了一些技巧避免分配内存
                            // 即使它大量使用了委托
                            // connectionBufferAddDelegate 委托会把接收到的节点加入 connectionBuffer
                            connectionBuffer = connectionBuffer ?? new List<GraphNode>();
                            connectionBufferAddDelegate = connectionBufferAddDelegate ??
                                                          (System.Action<GraphNode>)connectionBuffer.Add;

                            // 与起始节点或终止节点相邻的节点
                            adjacentNode = path.path[Mathf.Clamp(index + (start ? 1 : -1), 0, path.path.Count - 1)];

                            // 将 #node 的所有邻居添加到 connectionBuffer
                            node.GetConnections(connectionBufferAddDelegate);
                            var bestPos = nodePos;
                            var bestDist = float.PositiveInfinity;

                            // 遍历所有邻居
                            // 倒序循环，因为 connectionBuffer 的长度会在迭代中发生变化
                            for (int i = connectionBuffer.Count - 1; i >= 0; i--)
                            {
                                var neighbour = connectionBuffer[i];
                                if (!path.CanTraverse(neighbour)) continue;

                                // 找到该连接上的最近点
                                // 并检查它与目标点的距离是否比之前更小
                                var closest = VectorMath.ClosestPointOnSegment(nodePos, (Vector3)neighbour.position,
                                    relevantPoint);

                                var dist = (closest - relevantPoint).sqrMagnitude;
                                if (dist < bestDist)
                                {
                                    bestPos = closest;
                                    bestDist = dist;
                                    closestConnectionIndex = i;

                                    // 如果该节点不是相邻节点
                                    // 那么路径应当经过起始节点
                                    forceAddPoint = neighbour != adjacentNode;
                                }
                            }

                            connectionBuffer.Clear();
                            return bestPos;
                        default:
                            throw new System.ArgumentException(
                                "Cannot reach this point, but the compiler is not smart enough to realize that.");
                    }
                default:
                    throw new System.ArgumentException("Invalid mode");
            }
        }

        protected Vector3 GetClampedPoint(Vector3 from, Vector3 to, GraphNode hint)
        {
            Vector3 point = to;
            RaycastHit hit;

            if (useRaycasting && Physics.Linecast(from, to, out hit, mask))
            {
                point = hit.point;
            }

            if (useGraphRaycasting && hint != null)
            {
                var rayGraph = AstarData.GetGraph(hint) as IRaycastableGraph;

                if (rayGraph != null)
                {
                    GraphHitInfo graphHit;
                    if (rayGraph.Linecast(from, point, out graphHit))
                    {
                        point = graphHit.point;
                    }
                }
            }

            return point;
        }
    }
}