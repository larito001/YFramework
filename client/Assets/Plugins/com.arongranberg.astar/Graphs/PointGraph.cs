using UnityEngine;
using System.Collections.Generic;
using Pathfinding.Serialization;
using Pathfinding.Pooling;

namespace Pathfinding
{
    using Pathfinding.Drawing;
    using Unity.Jobs;

    /// <summary>
    /// 由一组点构成的图结构。
    ///
    /// [打开在线文档以查看示意图]
    ///
    /// PointGraph（点图）是最基本的图结构，由空间中若干互相连接的点组成，这些点被称为节点或路径点。
    /// PointGraph 使用一个 Transform 对象作为“root”（根节点），它会查找该 Transform 的所有子对象，每个子对象将作为一个节点处理。
    /// 如果启用了 <see cref="recursive"/>，则会递归地查找所有子对象的子对象。
    ///
    /// 然后会尝试在节点之间建立连接：
    /// 1. 首先检查两个节点之间的距离是否小于 <see cref="maxDistance"/>；
    /// 2. 接着检查轴对齐的距离是否满足限制，轴对齐距离由 <see cref="limits"/> 控制，
    ///    这对于 AI 无法攀爬过高地形的情况尤其有用，但仍然允许在相同 Y 高度的远距离节点之间建立连接。
    /// 注意：如果 <see cref="limits"/> 或 <see cref="maxDistance"/> 设置为 0，则表示无限制。
    ///
    /// 最后，会使用 <a href="http://unity3d.com/support/documentation/ScriptReference/Physics.Raycast.html">射线检测（Raycast）</a>
    /// 检查两个节点之间是否存在障碍物，也可以选择使用粗射线（Thick Raycast）。
    /// 使用射线检测时需要注意，应将节点略微放置在地面之上，或确保地面不在射线检测的遮罩中，以避免射线命中地面。
    ///
    /// 另外，也可以使用 Tag 标签来查找节点，
    /// 参考：https://docs.unity3d.com/Manual/Tags.html
    ///
    /// 对于较大的图结构，默认设置下扫描图可能耗时较长。
    /// 可以启用 <see cref="optimizeForSparseGraph"/> 来显著减少计算时间。
    ///
    /// 注意：不支持 Linecast，因为节点没有表面。
    ///
    /// 参见：get-started-point（请在在线文档中查看相关链接）
    /// 参见：graphTypes（请在在线文档中查看相关链接）
    ///
    /// \section pointgraph-inspector 检查器设置
    /// [打开在线文档以查看示意图]
    ///
    /// \inspectorField{根对象, root}
    /// \inspectorField{递归查找子对象, recursive}
    /// \inspectorField{查找节点的标签, searchTag}
    /// \inspectorField{最大连接距离, maxDistance}
    /// \inspectorField{轴对齐最大距离限制, limits}
    /// \inspectorField{启用射线检测, raycast}
    /// \inspectorField{射线检测 → 使用 2D 物理, use2DPhysics}
    /// \inspectorField{射线检测 → 启用粗射线, thickRaycast}
    /// \inspectorField{射线检测 → 粗射线 → 半径, thickRaycastRadius}
    /// \inspectorField{射线检测 → 遮罩, mask}
    /// \inspectorField{优化稀疏图, optimizeForSparseGraph}
    /// \inspectorField{最近节点查询使用最短距离, nearestNodeDistanceMode}
    /// \inspectorField{初始惩罚值, initialPenalty}
    /// </summary>
    [JsonOptIn]
    [Pathfinding.Util.Preserve]
    public class PointGraph : NavGraph
        , IUpdatableGraph
    {
        /// <summary>
        /// 此 Transform 的子对象将被视为节点。
        ///
        /// 如果为 null，则会改用 <see cref="searchTag"/> 进行查找。
        /// </summary>
        [JsonMember] public Transform root;

        /// <summary>If no <see cref="root"/> is set, all nodes with the tag is used as nodes</summary>
        [JsonMember] public string searchTag;

        /// <summary>
        /// 连接被视为有效的最大距离。
        /// 如果值为 0（零），将被视为无限距离，因此所有未受其他限制的节点都会建立连接。
        ///
        /// 如果值为负数，则不会添加任何邻接节点。
        /// 这将完全跳过连接处理过程，如果你不需要这些连接，可以节省计算资源。
        /// </summary>
        [JsonMember] public float maxDistance;

        /// <summary>
        /// 连接被视为有效的轴向最大距离。0 表示无限制。
        /// </summary>
        [JsonMember] public Vector3 limits;

        /// <summary>
        /// 使用射线检测来过滤连接。
        ///
        /// 如果在两个节点之间检测到碰撞，则不会建立连接。
        /// </summary>
        [JsonMember] public bool raycast = true;

        /// <summary>Use the 2D Physics API</summary>
        [JsonMember] public bool use2DPhysics;

        /// <summary>
        /// 使用粗射线检测。
        ///
        /// 如果启用，碰撞检测的形状将不是线段，而是一个半径为 <see cref="thickRaycastRadius"/> 的胶囊体。
        /// </summary>
        [JsonMember] public bool thickRaycast;

        /// <summary>
        /// Thick raycast radius.
        ///
        /// See: <see cref="thickRaycast"/>
        /// </summary>
        [JsonMember] public float thickRaycastRadius = 1;

        /// <summary>
        /// 递归查找 <see cref="root"/> 的子节点。
        ///
        /// 如果为 false，仅使用 <see cref="root"/> 的直接子对象作为节点。
        /// 如果为 true，则会递归地使用 <see cref="root"/> 的所有子对象及其后代作为节点。
        /// </summary>
        [JsonMember] public bool recursive = true;

        /// <summary>
        /// Layer mask to use for raycasting.
        ///
        /// All objects included in this layer mask will be treated as obstacles.
        ///
        /// See: <see cref="raycast"/>
        /// </summary>
        [JsonMember] public LayerMask mask;

        /// <summary>
        /// 针对稀疏图优化图结构。
        ///
        /// 这可以大幅减少扫描和普通路径请求的计算时间。
        /// 它减少了扫描过程中需要进行的节点间检查数量，同时也能优化从图中获取最近节点的操作（例如路径查询）。
        ///
        /// 你可以尝试启用或禁用此选项，通过扫描图时记录的扫描时间来判断你的图是否适合这种优化，
        /// 或者它是否会导致变慢。
        ///
        /// 使用此优化的效果会随着图规模增大而提升，默认扫描算法是暴力法，需要 O(n²) 次检查，
        /// 而此优化配合合适的图结构，扫描时仅需 O(n) 次检查（假设连接距离限制合理）。
        ///
        /// 警告：
        /// 启用此功能后，如果你通过脚本移动节点，必须同时重新计算查找结构，
        /// 详见：<see cref="RebuildNodeLookup"/>
        ///
        /// 如果你在运行时启用此选项，需要调用 <see cref="RebuildNodeLookup"/> 使其生效。
        /// 如果你随后要重新扫描图，则不需要调用此方法。
        /// </summary>
        [JsonMember] public bool optimizeForSparseGraph;

        PointKDTree lookupTree = new PointKDTree();

        /// <summary>
        /// 已知的最长连接距离。
        /// 以平方的 Int3 单位表示。
        ///
        /// 参见：<see cref="RegisterConnectionLength"/>
        /// </summary>
        long maximumConnectionLength = 0;

        /// <summary>
        /// 图中所有的节点。
        /// 注意，只有前面 <see cref="nodeCount"/> 个节点不会是 null。
        ///
        /// 你也可以使用 GetNodes 方法来获取所有节点。
        ///
        /// 节点的顺序未指定，并且在添加或删除节点时可能会改变。
        /// </summary>
        public PointNode[] nodes;

        /// <summary>
        /// \copydoc Pathfinding::PointGraph::NodeDistanceMode
        ///
        /// 参见：<see cref="NodeDistanceMode"/>
        ///
        /// 如果在运行时启用此功能，您需要调用 <see cref="RebuildConnectionDistanceLookup"/>
        /// 以确保某些缓存数据被正确地重新计算。
        /// 如果图中尚无节点或您打算随后扫描图，则无需执行此操作。
        /// </summary>
        [JsonMember] public NodeDistanceMode nearestNodeDistanceMode;

        /// <summary>Number of nodes in this graph</summary>
        public int nodeCount { get; protected set; }

        public override bool isScanned => nodes != null;

        /// <summary>
        /// 距离查询模式。
        /// [打开在线文档查看图片]
        ///
        /// 在上图中，有几个红色节点。假设代理是橙色圆圈。
        /// 使用“节点”模式，找到的最近点将是底部中央的节点，
        /// 这可能不是你想要的结果。使用“连接”模式，则会找到位于图像上半部分两个节点之间连接上的最近点。
        ///
        /// 当使用“连接”选项时，你可能还想为寻路器（Seeker）的起点终点修正器（Start End Modifier）的吸附选项使用“连接”模式。
        /// 虽然这不是绝对必要的，但在大多数情况下，这才是你想要的效果。
        ///
        /// 参见：<see cref="Pathfinding.StartEndModifier.exactEndPoint"/>
        /// </summary>
        public enum NodeDistanceMode
        {
            /// <summary>
            /// 所有最近节点查询都会找到最近的节点中心。
            /// 这是最快的选项，但如果存在较长的连接，可能不是你想要的结果。
            /// </summary>
            Node,

            /// <summary>
            /// 所有最近节点查询都会找到节点之间边缘上的最近点。
            /// 如果连接较长且代理可能站在两个节点之间的长连接上，从而更靠近某个无关节点时，这个模式非常有用。
            /// 但此模式比“节点”模式速度较慢。
            /// </summary>
            Connection,
        }

        public override int CountNodes()
        {
            return nodeCount;
        }

        public override void GetNodes(System.Action<GraphNode> action)
        {
            if (nodes == null) return;
            var count = nodeCount;
            for (int i = 0; i < count; i++) action(nodes[i]);
        }

        public override NNInfo GetNearest(Vector3 position, NNConstraint constraint, float maxDistanceSqr)
        {
            if (nodes == null) return NNInfo.Empty;
            var iposition = (Int3)position;

            if ((lookupTree != null) != optimizeForSparseGraph)
            {
                Debug.LogWarning(
                    "Lookup tree is not in the correct state. Have you changed PointGraph.optimizeForSparseGraph without calling RebuildNodeLookup?");
            }

            if (lookupTree != null)
            {
                if (nearestNodeDistanceMode == NodeDistanceMode.Node)
                {
                    var minDistSqr = maxDistanceSqr;
                    var closestNode = lookupTree.GetNearest(iposition, constraint, ref minDistSqr);
                    return closestNode == null
                        ? NNInfo.Empty
                        : new NNInfo(closestNode, (Vector3)closestNode.position, minDistSqr);
                }
                else
                {
                    var closestNode = lookupTree.GetNearestConnection(iposition, constraint, maximumConnectionLength);
                    return closestNode == null
                        ? NNInfo.Empty
                        : FindClosestConnectionPoint(closestNode as PointNode, position, maxDistanceSqr);
                }
            }

            PointNode minNode = null;
            long minDist =
                AstarMath.SaturatingConvertFloatToLong(maxDistanceSqr * Int3.FloatPrecision * Int3.FloatPrecision);

            for (int i = 0; i < nodeCount; i++)
            {
                PointNode node = nodes[i];
                long dist = (iposition - node.position).sqrMagnitudeLong;

                if (dist < minDist && (constraint == null || constraint.Suitable(node)))
                {
                    minDist = dist;
                    minNode = node;
                }
            }

            float distSqr = Int3.PrecisionFactor * Int3.PrecisionFactor * minDist;
            // Do a final distance check here just to make sure we don't exceed the max distance due to rounding errors when converting between longs and floats
            return distSqr < maxDistanceSqr && minNode != null
                ? new NNInfo(minNode, (Vector3)minNode.position, Int3.PrecisionFactor * Int3.PrecisionFactor * minDist)
                : NNInfo.Empty;
        }

        NNInfo FindClosestConnectionPoint(PointNode node, Vector3 position, float maxDistanceSqr)
        {
            var closestConnectionPoint = (Vector3)node.position;
            var conns = node.connections;
            var nodePos = (Vector3)node.position;

            if (conns != null)
            {
                for (int i = 0; i < conns.Length; i++)
                {
                    var connectionMidpoint = ((UnityEngine.Vector3)conns[i].node.position + nodePos) * 0.5f;
                    var closestPoint = VectorMath.ClosestPointOnSegment(nodePos, connectionMidpoint, position);
                    var dist = (closestPoint - position).sqrMagnitude;
                    if (dist < maxDistanceSqr)
                    {
                        maxDistanceSqr = dist;
                        closestConnectionPoint = closestPoint;
                    }
                }
            }

            return new NNInfo(node, closestConnectionPoint, maxDistanceSqr);
        }

        public override NNInfo RandomPointOnSurface(NNConstraint nnConstraint = null, bool highQuality = true)
        {
            if (!isScanned || nodes.Length == 0) return NNInfo.Empty;

            // 所有节点的表面面积相同，因此直接随机选择一个节点即可
            for (int i = 0; i < 10; i++)
            {
                var node = this.nodes[UnityEngine.Random.Range(0, this.nodes.Length)];
                if (node != null && (nnConstraint == null || nnConstraint.Suitable(node)))
                {
                    return new NNInfo(node, node.RandomPointOnSurface(), 0);
                }
            }

            // 如果经过几次尝试仍未找到有效节点，则图中很可能包含大量不可通行或不合适的节点。
            // 回退到基类方法，该方法会通过检查所有节点来尝试找到有效节点。
            return base.RandomPointOnSurface(nnConstraint, highQuality);
        }

        /// <summary>
        /// 在指定位置向图中添加一个节点。
        /// 注意：Vector3 可以通过 (Int3)myVector 转换为 Int3。
        ///
        /// 注意：此方法需要在安全更新节点的时机调用，时机包括：
        /// - 扫描期间
        /// - 图更新期间
        /// - 在使用 AstarPath.AddWorkItem 注册的回调内部
        ///
        /// <code>
        /// AstarPath.active.AddWorkItem(() => {
        ///     var graph = AstarPath.active.data.pointGraph;
        ///     // 添加两个节点并连接它们
        ///     var node1 = graph.AddNode((Int3)transform.position);
        ///     var node2 = graph.AddNode((Int3)(transform.position + Vector3.right));
        ///     var cost = (uint)(node2.position - node1.position).costMagnitude;
        ///     GraphNode.Connect(node1, node2, cost);
        /// });
        /// </code>
        ///
        /// 参见：runtime-graphs（查看在线文档中的有效链接）
        /// 参见：creating-point-nodes（查看在线文档中的有效链接）
        /// </summary>
        public PointNode AddNode(Int3 position)
        {
            return AddNode(new PointNode(active), position);
        }

        /// <summary>
        /// 在指定位置向图中添加一个指定类型的节点。
        ///
        /// 注意：Vector3 可以通过 (Int3)myVector 转换为 Int3。
        ///
        /// 注意：此方法需要在安全更新节点的时机调用，时机包括：
        /// - 扫描期间
        /// - 图更新期间
        /// - 在使用 AstarPath.AddWorkItem 注册的回调内部
        ///
        /// 参见：<see cref="AstarPath.AddWorkItem"/>
        /// 参见：runtime-graphs（查看在线文档中的有效链接）
        /// 参见：creating-point-nodes（查看在线文档中的有效链接）
        /// </summary>
        /// <param name="node">该节点必须是在调用此方法之前使用 T(AstarPath.active) 创建的节点。
        /// 传入该参数是因为泛型类型参数上没有 new(AstarPath) 约束。</param>
        /// <param name="position">节点将被设置到该位置。</param>
        public T AddNode<T>(T node, Int3 position) where T : PointNode
        {
            AssertSafeToUpdateGraph();
            if (nodes == null || nodeCount == nodes.Length)
            {
                var newNodes = new PointNode[nodes != null ? System.Math.Max(nodes.Length + 4, nodes.Length * 2) : 4];
                if (nodes != null) nodes.CopyTo(newNodes, 0);
                nodes = newNodes;
                RebuildNodeLookup();
            }

            node.position = position;
            node.GraphIndex = graphIndex;
            node.Walkable = true;

            nodes[nodeCount] = node;
            nodeCount++;

            if (lookupTree != null) lookupTree.Add(node);

            return node;
        }

        /// <summary>
        /// 从图中移除一个节点。
        ///
        /// <code>
        /// // 确保只在所有寻路线程暂停时修改图
        /// AstarPath.active.AddWorkItem(() => {
        ///     // 找到距离某点最近的节点
        ///     var nearest = AstarPath.active.GetNearest(new Vector3(1, 2, 3));
        ///
        ///     // 检查是否为 PointNode 类型
        ///     if (nearest.node is PointNode pnode) {
        ///         // 移除该节点。假设它属于场景中的第一个点图
        ///         AstarPath.active.data.pointGraph.RemoveNode(pnode);
        ///     }
        /// });
        /// </code>
        ///
        /// 注意：对于较大的图，该操作可能较慢，因为它的时间复杂度是线性于图中节点数量的。
        ///
        /// 参见：<see cref="AddNode"/>
        /// 参见：creating-point-nodes（查看在线文档中的有效链接）
        /// </summary>
        public void RemoveNode(PointNode node)
        {
            AssertSafeToUpdateGraph();
            if (node.Destroyed) throw new System.ArgumentException("The node has already been destroyed");
            if (node.GraphIndex != graphIndex)
                throw new System.ArgumentException("The node does not belong to this graph");
            if (!isScanned) throw new System.InvalidOperationException("Graph contains no nodes");

            // 移除节点并与最后一个节点交换位置
            // 这样做是因为我们不保证节点的顺序
            var idx = System.Array.IndexOf(nodes, node);
            if (idx == -1) throw new System.ArgumentException("The node is not in the graph");

            nodeCount--;
            nodes[idx] = nodes[nodeCount];
            nodes[nodeCount] = null;
            node.Destroy();

            if (lookupTree != null)
            {
                lookupTree.Remove(node);
            }
        }

        /// <summary>递归计算一个Transform的子对象数量</summary>
        protected static int CountChildren(Transform tr)
        {
            int c = 0;

            foreach (Transform child in tr)
            {
                c++;
                c += CountChildren(child);
            }

            return c;
        }

        /// <summary>递归将一个Transform的所有子对象添加为节点</summary>
        protected static void AddChildren(PointNode[] nodes, ref int c, Transform tr)
        {
            foreach (Transform child in tr)
            {
                nodes[c].position = (Int3)child.position;
                nodes[c].Walkable = true;
                nodes[c].gameObject = child.gameObject;

                c++;
                AddChildren(nodes, ref c, child);
            }
        }

        /// <summary>
        /// 重建节点的查找结构。
        ///
        /// 当启用 <see cref="optimizeForSparseGraph"/> 时使用。
        ///
        /// 每当你手动移动图中的节点并且使用了 <see cref="optimizeForSparseGraph"/> 时，
        /// 都应调用此方法，否则寻路可能无法正常工作。
        ///
        /// 你也可以在使用 <see cref="AddNode"/> 方法添加了大量节点之后调用此方法。
        /// 通过 <see cref="AddNode"/> 添加节点时，它们会被添加到查找结构中。
        /// 当查找结构变得不平衡时会自动重新平衡。
        /// 但是如果你确定短期内不会再添加节点，
        /// 可以调用此方法确保查找结构达到完美平衡，
        /// 从而挤出最后一点性能提升。
        /// 这可以略微提升 <see cref="GetNearest"/> 方法的性能，
        /// 提升幅度约为 10-20%。
        /// </summary>
        public void RebuildNodeLookup()
        {
            lookupTree = BuildNodeLookup(nodes, nodeCount, optimizeForSparseGraph);
            RebuildConnectionDistanceLookup();
        }

        static PointKDTree BuildNodeLookup(PointNode[] nodes, int nodeCount, bool optimizeForSparseGraph)
        {
            if (optimizeForSparseGraph && nodes != null)
            {
                var lookupTree = new PointKDTree();
                lookupTree.Rebuild(nodes, 0, nodeCount);
                return lookupTree;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 重建缓存，用于当 <see cref="nearestNodeDistanceMode"/> = <see cref="NodeDistanceMode"/>.Connection 时。
        /// </summary>
        public void RebuildConnectionDistanceLookup()
        {
            if (nearestNodeDistanceMode == NodeDistanceMode.Connection)
            {
                maximumConnectionLength = LongestConnectionLength(nodes, nodeCount);
            }
            else
            {
                maximumConnectionLength = 0;
            }
        }

        static long LongestConnectionLength(PointNode[] nodes, int nodeCount)
        {
            long maximumConnectionLength = 0;
            for (int j = 0; j < nodeCount; j++)
            {
                var node = nodes[j];
                var conns = node.connections;
                if (conns != null)
                {
                    for (int i = 0; i < conns.Length; i++)
                    {
                        var distSqr = (node.position - conns[i].node.position).sqrMagnitudeLong;
                        maximumConnectionLength = System.Math.Max(maximumConnectionLength, distSqr);
                    }
                }
            }

            return maximumConnectionLength;
        }

        /// <summary>
        /// 确保图知道存在此长度的连接。
        /// 当最近节点距离模式设置为 Connection 时使用。
        /// 如果你自己修改节点连接（例如操作 PointNode.connections 数组），添加任何连接时必须调用此函数。
        ///
        /// 使用 GraphNode.Connect 时会自动调用此函数。
        /// 在调用 <see cref="RebuildNodeLookup"/> 时也会对所有节点执行此操作。
        /// </summary>
        /// <param name="sqrLength">连接的长度，以平方 Int3 单位表示。可通过 (node1.position - node2.position).sqrMagnitudeLong 计算。</param>
        public void RegisterConnectionLength(long sqrLength)
        {
            maximumConnectionLength = System.Math.Max(maximumConnectionLength, sqrLength);
        }

        protected virtual PointNode[] CreateNodes(int count)
        {
            var nodes = new PointNode[count];

            for (int i = 0; i < count; i++) nodes[i] = new PointNode(active);
            return nodes;
        }

        class PointGraphScanPromise : IGraphUpdatePromise
        {
            public PointGraph graph;
            PointKDTree lookupTree;
            PointNode[] nodes;

            public IEnumerator<JobHandle> Prepare()
            {
                var root = graph.root;
                if (root == null)
                {
                    // If there is no root object, try to find nodes with the specified tag instead
                    GameObject[] gos = graph.searchTag != null
                        ? GameObject.FindGameObjectsWithTag(graph.searchTag)
                        : null;

                    if (gos == null)
                    {
                        nodes = new PointNode[0];
                    }
                    else
                    {
                        // Create all the nodes
                        nodes = graph.CreateNodes(gos.Length);

                        for (int i = 0; i < gos.Length; i++)
                        {
                            var node = nodes[i];
                            node.position = (Int3)gos[i].transform.position;
                            node.Walkable = true;
                            node.gameObject = gos[i].gameObject;
                        }
                    }
                }
                else
                {
                    // Search the root for children and create nodes for them
                    if (!graph.recursive)
                    {
                        var nodeCount = root.childCount;
                        nodes = graph.CreateNodes(nodeCount);

                        int c = 0;
                        foreach (Transform child in root)
                        {
                            var node = nodes[c];
                            node.position = (Int3)child.position;
                            node.Walkable = true;
                            node.gameObject = child.gameObject;
                            c++;
                        }
                    }
                    else
                    {
                        var nodeCount = CountChildren(root);
                        nodes = graph.CreateNodes(nodeCount);

                        int nodeIndex = 0;
                        AddChildren(nodes, ref nodeIndex, root);
                        UnityEngine.Assertions.Assert.AreEqual(nodeIndex, nodeCount);
                    }
                }

                yield return default;
                lookupTree = BuildNodeLookup(nodes, nodes.Length, graph.optimizeForSparseGraph);

                foreach (var progress in ConnectNodesAsync(nodes, nodes.Length, lookupTree, graph.maxDistance,
                             graph.limits, graph)) yield return default;
            }

            public void Apply(IGraphUpdateContext ctx)
            {
                // Destroy all previous nodes (if any)
                graph.DestroyAllNodes();
                // Assign the new node data
                graph.lookupTree = lookupTree;
                graph.nodes = nodes;
                graph.nodeCount = nodes.Length;
                graph.maximumConnectionLength = graph.nearestNodeDistanceMode == NodeDistanceMode.Connection
                    ? LongestConnectionLength(nodes, nodes.Length)
                    : 0;
            }
        }

        protected override void DestroyAllNodes()
        {
            base.DestroyAllNodes();
            nodes = null;
            lookupTree = null;
        }

        protected override IGraphUpdatePromise ScanInternal() => new PointGraphScanPromise { graph = this };

        /// <summary>
        /// 重新计算图中所有节点的连接。
        /// 如果你使用 <see cref="AddNode"/> 手动创建了节点，并希望以点图通常连接节点的方式连接它们，则此方法非常有用。
        /// </summary>
        public void ConnectNodes()
        {
            AssertSafeToUpdateGraph();
            var ie = ConnectNodesAsync(nodes, nodeCount, lookupTree, maxDistance, limits, this).GetEnumerator();

            while (ie.MoveNext())
            {
            }

            RebuildConnectionDistanceLookup();
        }

        /// <summary>
        /// 计算图中所有节点的连接。
        /// 这是一个 IEnumerable，可以使用 foreach 等方式进行迭代，以获取进度信息。
        /// </summary>
        static IEnumerable<float> ConnectNodesAsync(PointNode[] nodes, int nodeCount, PointKDTree lookupTree,
            float maxDistance, Vector3 limits, PointGraph graph)
        {
            if (maxDistance >= 0)
            {
                // To avoid too many allocations, these lists are reused for each node
                var connections = new List<Connection>();
                var candidateConnections = new List<GraphNode>();

                long maxSquaredRange;
                // 两个节点之间连接的最大可能平方距离
                // 用于加速计算，通过跳过许多不需要检查的节点

                if (maxDistance == 0 && (limits.x == 0 || limits.y == 0 || limits.z == 0))
                {
                    maxSquaredRange = long.MaxValue;
                }
                else
                {
                    maxSquaredRange =
                        (long)(Mathf.Max(limits.x, Mathf.Max(limits.y, Mathf.Max(limits.z, maxDistance))) *
                               Int3.Precision) + 1;
                    maxSquaredRange *= maxSquaredRange;
                }

                // Report progress every N nodes
                const int YieldEveryNNodes = 512;

                // Loop through all nodes and add connections to other nodes
                for (int i = 0; i < nodeCount; i++)
                {
                    if (i % YieldEveryNNodes == 0)
                    {
                        yield return i / (float)nodeCount;
                    }

                    connections.Clear();
                    var node = nodes[i];
                    if (lookupTree != null)
                    {
                        candidateConnections.Clear();
                        lookupTree.GetInRange(node.position, maxSquaredRange, candidateConnections);
                        for (int j = 0; j < candidateConnections.Count; j++)
                        {
                            var other = candidateConnections[j] as PointNode;
                            if (other != node && graph.IsValidConnection(node, other, out var dist))
                            {
                                connections.Add(new Connection(
                                    other,
                                    /// <summary>TODO: Is this equal to .costMagnitude</summary>
                                    (uint)Mathf.RoundToInt(dist * Int3.FloatPrecision),
                                    true,
                                    true
                                ));
                            }
                        }
                    }
                    else
                    {
                        // brute force
                        for (int j = 0; j < nodeCount; j++)
                        {
                            if (i == j) continue;

                            PointNode other = nodes[j];
                            if (graph.IsValidConnection(node, other, out var dist))
                            {
                                connections.Add(new Connection(
                                    other,
                                    /// <summary>TODO: Is this equal to .costMagnitude</summary>
                                    (uint)Mathf.RoundToInt(dist * Int3.FloatPrecision),
                                    true,
                                    true
                                ));
                            }
                        }
                    }

                    node.connections = connections.ToArray();
                    node.SetConnectivityDirty();
                }
            }
        }

        /// <summary>
        /// 判断节点 a 和 b 之间的连接是否有效。
        /// 会使用射线检测（如果启用）来检查障碍物，并检查高度差异。
        /// 另外，如果连接有效，还会输出两个节点之间的距离。
        ///
        /// 注意：这和检查节点 a 是否连接到节点 b 不同，
        /// 后者应该使用 a.ContainsOutgoingConnection(b) 来判断。
        /// </summary>
        public virtual bool IsValidConnection(GraphNode a, GraphNode b, out float dist)
        {
            dist = 0;

            if (!a.Walkable || !b.Walkable) return false;

            var dir = (Vector3)(b.position - a.position);

            if (
                (!Mathf.Approximately(limits.x, 0) && Mathf.Abs(dir.x) > limits.x) ||
                (!Mathf.Approximately(limits.y, 0) && Mathf.Abs(dir.y) > limits.y) ||
                (!Mathf.Approximately(limits.z, 0) && Mathf.Abs(dir.z) > limits.z))
            {
                return false;
            }

            dist = dir.magnitude;
            if (maxDistance == 0 || dist < maxDistance)
            {
                if (raycast)
                {
                    var ray = new Ray((Vector3)a.position, dir);
                    var invertRay = new Ray((Vector3)b.position, -dir);

                    if (use2DPhysics)
                    {
                        if (thickRaycast)
                        {
                            return !Physics2D.CircleCast(ray.origin, thickRaycastRadius, ray.direction, dist, mask) &&
                                   !Physics2D.CircleCast(invertRay.origin, thickRaycastRadius, invertRay.direction,
                                       dist, mask);
                        }
                        else
                        {
                            return !Physics2D.Linecast((Vector2)(Vector3)a.position, (Vector2)(Vector3)b.position,
                                mask) && !Physics2D.Linecast((Vector2)(Vector3)b.position, (Vector2)(Vector3)a.position,
                                mask);
                        }
                    }
                    else
                    {
                        if (thickRaycast)
                        {
                            return !Physics.SphereCast(ray, thickRaycastRadius, dist, mask) &&
                                   !Physics.SphereCast(invertRay, thickRaycastRadius, dist, mask);
                        }
                        else
                        {
                            return !Physics.Linecast((Vector3)a.position, (Vector3)b.position, mask) &&
                                   !Physics.Linecast((Vector3)b.position, (Vector3)a.position, mask);
                        }
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        class PointGraphUpdatePromise : IGraphUpdatePromise
        {
            public PointGraph graph;
            public List<GraphUpdateObject> graphUpdates;

            public void Apply(IGraphUpdateContext ctx)
            {
                var nodes = graph.nodes;
                for (int u = 0; u < graphUpdates.Count; u++)
                {
                    var guo = graphUpdates[u];
                    for (int i = 0; i < graph.nodeCount; i++)
                    {
                        var node = nodes[i];
                        if (guo.bounds.Contains((Vector3)node.position))
                        {
                            guo.WillUpdateNode(node);
                            guo.Apply(node);
                        }
                    }

                    if (guo.updatePhysics)
                    {
                        // Use a copy of the bounding box, we should not change the GUO's bounding box since it might be used for other graph updates
                        Bounds bounds = guo.bounds;

                        if (graph.thickRaycast)
                        {
                            // Expand the bounding box to account for the thick raycast
                            bounds.Expand(graph.thickRaycastRadius * 2);
                        }

                        // Create a temporary list used for holding connection data
                        List<Connection> tmpList = Pathfinding.Pooling.ListPool<Connection>.Claim();

                        for (int i = 0; i < graph.nodeCount; i++)
                        {
                            PointNode node = graph.nodes[i];
                            var nodePos = (Vector3)node.position;

                            List<Connection> conn = null;

                            for (int j = 0; j < graph.nodeCount; j++)
                            {
                                if (j == i) continue;

                                var otherNodePos = (Vector3)nodes[j].position;
                                // Check if this connection intersects the bounding box.
                                // If it does we need to recalculate that connection.
                                if (VectorMath.SegmentIntersectsBounds(bounds, nodePos, otherNodePos))
                                {
                                    float dist;
                                    PointNode other = nodes[j];
                                    bool contains = node.ContainsOutgoingConnection(other);
                                    bool validConnection = graph.IsValidConnection(node, other, out dist);

                                    // Fill the 'conn' list when we need to change a connection
                                    if (conn == null && (contains != validConnection))
                                    {
                                        tmpList.Clear();
                                        conn = tmpList;
                                        if (node.connections != null) conn.AddRange(node.connections);
                                    }

                                    if (!contains && validConnection)
                                    {
                                        // A new connection should be added
                                        uint cost = (uint)Mathf.RoundToInt(dist * Int3.FloatPrecision);
                                        conn.Add(new Connection(other, cost, true, true));
                                        graph.RegisterConnectionLength(
                                            (other.position - node.position).sqrMagnitudeLong);
                                    }
                                    else if (contains && !validConnection)
                                    {
                                        // A connection should be removed
                                        for (int q = 0; q < conn.Count; q++)
                                        {
                                            if (conn[q].node == other)
                                            {
                                                conn.RemoveAt(q);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            // Save the new connections if any were changed
                            if (conn != null)
                            {
                                node.connections = conn.ToArray();
                                node.SetConnectivityDirty();
                            }
                        }

                        // Release buffers back to the pool
                        ListPool<Connection>.Release(ref tmpList);
                        ctx.DirtyBounds(guo.bounds);
                    }
                }

                ListPool<GraphUpdateObject>.Release(ref graphUpdates);
            }
        }

        /// <summary>
        /// 更新列表图中的一个区域。
        /// 重新计算可能受影响的连接，即所有穿过该区域边界的连接线将被重新计算。
        /// </summary>
        IGraphUpdatePromise IUpdatableGraph.ScheduleGraphUpdates(List<GraphUpdateObject> graphUpdates)
        {
            if (!isScanned) return null;

            return new PointGraphUpdatePromise
            {
                graph = this,
                graphUpdates = graphUpdates
            };
        }

#if UNITY_EDITOR
        static readonly Color NodeColor = new Color(0.161f, 0.341f, 1f, 0.5f);

        public override void OnDrawGizmos(DrawingData gizmos, bool drawNodes, RedrawScope redrawScope)
        {
            base.OnDrawGizmos(gizmos, drawNodes, redrawScope);

            if (!drawNodes) return;

            using (var draw = gizmos.GetBuilder())
            {
                using (draw.WithColor(NodeColor))
                {
                    if (this.isScanned)
                    {
                        for (int i = 0; i < nodeCount; i++)
                        {
                            var pos = (Vector3)nodes[i].position;
                            draw.SolidBox(pos, Vector3.one * UnityEditor.HandleUtility.GetHandleSize(pos) * 0.1F);
                        }
                    }
                    else
                    {
                        // When not scanned, draw the source data
                        if (root != null)
                        {
                            DrawChildren(draw, this, root);
                        }
                        else if (!string.IsNullOrEmpty(searchTag))
                        {
                            GameObject[] gos = GameObject.FindGameObjectsWithTag(searchTag);
                            for (int i = 0; i < gos.Length; i++)
                            {
                                draw.SolidBox(gos[i].transform.position,
                                    Vector3.one * UnityEditor.HandleUtility.GetHandleSize(gos[i].transform.position) *
                                    0.1F);
                            }
                        }
                    }
                }
            }
        }

        static void DrawChildren(CommandBuilder draw, PointGraph graph, Transform tr)
        {
            foreach (Transform child in tr)
            {
                draw.SolidBox(child.position,
                    Vector3.one * UnityEditor.HandleUtility.GetHandleSize(child.position) * 0.1F);
                if (graph.recursive) DrawChildren(draw, graph, child);
            }
        }
#endif

        protected override void PostDeserialization(GraphSerializationContext ctx)
        {
            RebuildNodeLookup();
        }

        public override void RelocateNodes(Matrix4x4 deltaMatrix)
        {
            base.RelocateNodes(deltaMatrix);
            RebuildNodeLookup();
        }

        protected override void SerializeExtraInfo(GraphSerializationContext ctx)
        {
            // Serialize node data

            if (nodes == null) ctx.writer.Write(-1);

            // Length prefixed array of nodes
            ctx.writer.Write(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                // -1 indicates a null field
                if (nodes[i] == null) ctx.writer.Write(-1);
                else
                {
                    ctx.writer.Write(0);
                    nodes[i].SerializeNode(ctx);
                }
            }
        }

        protected override void DeserializeExtraInfo(GraphSerializationContext ctx)
        {
            int count = ctx.reader.ReadInt32();

            if (count == -1)
            {
                nodes = null;
                return;
            }

            nodes = new PointNode[count];
            nodeCount = count;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (ctx.reader.ReadInt32() == -1) continue;
                nodes[i] = new PointNode(active);
                nodes[i].DeserializeNode(ctx);
            }
        }
    }
}