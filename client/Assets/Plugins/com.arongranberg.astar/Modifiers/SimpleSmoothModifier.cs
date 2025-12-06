using UnityEngine;
using System.Collections.Generic;
using Pathfinding.Pooling;

namespace Pathfinding
{
    /// <summary>
    /// 用于平滑路径的修改器。这个修改器可以通过两种方式平滑路径：将点靠得更近（Simple）或使用贝塞尔曲线（Bezier）。
    ///
    /// 将此组件附加到与 Seeker 组件相同的 GameObject 上。
    ///
    /// 此组件会挂接到 Seeker 的路径后处理系统，并对搜索到的路径进行后处理。
    /// 可以查看 Seeker 组件上的 Modifier Priorities（修改器优先级）设置，以确定此修改器在处理路径的流程中应处于哪个位置。
    ///
    /// 提供了几种平滑类型，下面列出了它们及其简短说明，包括作用和工作原理。
    /// 但最好的方法还是自己动手实验。
    ///
    /// - <b>Simple</b> 通过将所有点靠近来平滑路径。如果不小心，这可能导致路径“切角”。  
    /// 它还会对子路径进行细分以创建更多的点以进行平滑，否则路径仍会比较粗糙。  
    /// [打开在线文档查看图片]
    /// - <b>Bezier</b> 使用贝塞尔曲线平滑路径。这样得到的路径会非常平滑，并且总是通过路径中的所有点，但要确保路径不会转得太快。  
    /// [打开在线文档查看图片]
    /// - <b>OffsetSimple</b> Simple 平滑的一种替代方法，每一步都会将路径向外偏移以尽量减少切角。  
    /// 但要小心，如果数值过大，会形成环路，看起来非常不美观。
    /// - <b>Curved Non Uniform</b> [打开在线文档查看图片]
    ///
    /// 注意：会修改 vectorPath 数组
    /// TODO：让平滑修改器在平滑路径时考虑世界几何形状
    /// </summary>
    [AddComponentMenu("Pathfinding/Modifiers/Simple Smooth Modifier")]
    [System.Serializable]
    [RequireComponent(typeof(Seeker))]
    [HelpURL("https://arongranberg.com/astar/documentation/stable/simplesmoothmodifier.html")]
    public class SimpleSmoothModifier : MonoModifier
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Seeker/Add Simple Smooth Modifier")]
        public static void AddComp(UnityEditor.MenuCommand command)
        {
            (command.context as Component).gameObject.AddComponent(typeof(SimpleSmoothModifier));
        }
#endif

        public override int Order
        {
            get { return 50; }
        }

        /// <summary>使用的平滑类型</summary>
        public SmoothType smoothType = SmoothType.Simple;

        /// <summary>当不使用均匀长度时，对路径进行细分的次数</summary>
        [Tooltip("细分路径段的次数 [0...无限] (推荐 [1...10])")]
        public int subdivisions = 2;

        /// <summary>应用平滑操作的次数</summary>
        [Tooltip("应用平滑的迭代次数")] public int iterations = 2;

        /// <summary>每次迭代平滑时的强度。通常 0.5 可以得到最自然的曲线效果</summary>
        [Tooltip("每次迭代平滑时应用的强度。0.5 通常产生最美观的曲线")] [Range(0, 1)]
        public float strength = 0.5F;

        /// <summary>是否将路径段分割为等长段</summary>
        [Tooltip("是否将路径的所有线段分割为等长段")] public bool uniformLength = true;

        /// <summary>在使用 <see cref="uniformLength"/> 时，每段路径的最大长度。
        /// 较大的值会得到粗糙路径，较小的值会得到非常平滑的路径，但速度较慢</summary>
        [Tooltip("平滑路径中每段的长度。高值路径粗糙，低值路径平滑，但速度较慢")]
        public float maxSegmentLength = 2F;

        /// <summary>贝塞尔曲线切线长度因子</summary>
        [Tooltip("贝塞尔曲线切线的长度因子")] public float bezierTangentLength = 0.4F;

        /// <summary>在使用 Offset Simple 时，每次迭代应用的偏移量</summary>
        [Tooltip("使用 Offset Simple 平滑时，每次迭代应用的偏移量")]
        public float offset = 0.2F;


        /// <summary>Curved Non Uniform 平滑使用的圆滑因子</summary>
        [Tooltip("路径平滑程度。数值越大路径越平滑，但可能偏离最优路径")]
        public float factor = 0.1F;

        public enum SmoothType
        {
            Simple,//简易线性平滑
            Bezier,//贝塞尔曲线
            OffsetSimple,
            CurvedNonuniform
        }

        public override void Apply(Path p)
        {
            // This should never trigger unless some other modifier has messed stuff up
            if (p.vectorPath == null)
            {
                Debug.LogWarning("无法处理空路径（是否有其他修改器报错？）");
                return;
            }

            List<Vector3> path = null;

            switch (smoothType)
            {
                case SmoothType.Simple:
                    path = SmoothSimple(p.vectorPath);
                    break;
                case SmoothType.Bezier:
                    path = SmoothBezier(p.vectorPath);
                    break;
                case SmoothType.OffsetSimple:
                    path = SmoothOffsetSimple(p.vectorPath);
                    break;
                case SmoothType.CurvedNonuniform:
                    path = CurvedNonuniform(p.vectorPath);
                    break;
            }

            if (path != p.vectorPath)
            {
                ListPool<Vector3>.Release(ref p.vectorPath);
                p.vectorPath = path;
            }
        }

        /// <summary>非均匀曲线平滑实现</summary>
        public List<Vector3> CurvedNonuniform(List<Vector3> path)
        {
            if (maxSegmentLength <= 0)
            {
                Debug.LogWarning("最大段长度 <= 0 会导致除零或其他错误（避免这种情况）");
                return path;
            }

            int pointCounter = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                //pointCounter += Mathf.FloorToInt ((path[i]-path[i+1]).magnitude / maxSegmentLength)+1;

                float dist = (path[i] - path[i + 1]).magnitude;
                //In order to avoid floating point errors as much as possible, and in lack of a better solution
                //loop through it EXACTLY as the other code further down will
                for (float t = 0; t <= dist; t += maxSegmentLength)
                {
                    pointCounter++;
                }
            }

            List<Vector3> subdivided = ListPool<Vector3>.Claim(pointCounter);

            // Set first velocity
            Vector3 preEndVel = (path[1] - path[0]).normalized;

            for (int i = 0; i < path.Count - 1; i++)
            {
                float dist = (path[i] - path[i + 1]).magnitude;

                Vector3 startVel1 = preEndVel;
                Vector3 endVel1 = i < path.Count - 2
                    ? ((path[i + 2] - path[i + 1]).normalized - (path[i] - path[i + 1]).normalized).normalized
                    : (path[i + 1] - path[i]).normalized;

                Vector3 startVel = startVel1 * dist * factor;
                Vector3 endVel = endVel1 * dist * factor;

                Vector3 start = path[i];
                Vector3 end = path[i + 1];

                float onedivdist = 1F / dist;

                for (float t = 0; t <= dist; t += maxSegmentLength)
                {
                    float t2 = t * onedivdist;

                    subdivided.Add(GetPointOnCubic(start, end, startVel, endVel, t2));
                }

                preEndVel = endVel1;
            }

            subdivided[subdivided.Count - 1] = path[path.Count - 1];

            return subdivided;
        }

        /// <summary>计算三次贝塞尔曲线上的点</summary>
        public static Vector3 GetPointOnCubic(Vector3 a, Vector3 b, Vector3 tan1, Vector3 tan2, float t)
        {
            float t2 = t * t, t3 = t2 * t;

            float h1 = 2 * t3 - 3 * t2 + 1; // calculate basis function 1
            float h2 = -2 * t3 + 3 * t2; // calculate basis function 2
            float h3 = t3 - 2 * t2 + t; // calculate basis function 3
            float h4 = t3 - t2; // calculate basis function 4

            return h1 * a + // multiply and sum all funtions
                   h2 * b + // together to build the interpolated
                   h3 * tan1 + // point along the curve.
                   h4 * tan2;
        }

        /// <summary>Offset Simple 平滑实现</summary>
        public List<Vector3> SmoothOffsetSimple(List<Vector3> path)
        {
            if (path.Count <= 2 || iterations <= 0)
            {
                return path;
            }

            if (iterations > 12)
            {
                Debug.LogWarning("A very high iteration count was passed, won't let this one through");
                return path;
            }

            int maxLength = (path.Count - 2) * (int)Mathf.Pow(2, iterations) + 2;

            List<Vector3> subdivided = ListPool<Vector3>.Claim(maxLength);
            List<Vector3> subdivided2 = ListPool<Vector3>.Claim(maxLength);

            for (int i = 0; i < maxLength; i++)
            {
                subdivided.Add(Vector3.zero);
                subdivided2.Add(Vector3.zero);
            }

            for (int i = 0; i < path.Count; i++)
            {
                subdivided[i] = path[i];
            }

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int currentPathLength = (path.Count - 2) * (int)Mathf.Pow(2, iteration) + 2;

                //Switch the arrays
                List<Vector3> tmp = subdivided;
                subdivided = subdivided2;
                subdivided2 = tmp;

                const float nextMultiplier = 1F;

                for (int i = 0; i < currentPathLength - 1; i++)
                {
                    Vector3 current = subdivided2[i];
                    Vector3 next = subdivided2[i + 1];

                    Vector3 normal = Vector3.Cross(next - current, Vector3.up);
                    normal = normal.normalized;

                    bool firstRight = false;
                    bool secondRight = false;
                    bool setFirst = false;
                    bool setSecond = false;
                    if (i != 0 && !VectorMath.IsColinearXZ(current, next, subdivided2[i - 1]))
                    {
                        setFirst = true;
                        firstRight = VectorMath.RightOrColinearXZ(current, next, subdivided2[i - 1]);
                    }

                    if (i < currentPathLength - 1 && !VectorMath.IsColinearXZ(current, next, subdivided2[i + 2]))
                    {
                        setSecond = true;
                        secondRight = VectorMath.RightOrColinearXZ(current, next, subdivided2[i + 2]);
                    }

                    if (setFirst)
                    {
                        subdivided[i * 2] = current +
                                            (firstRight
                                                ? normal * offset * nextMultiplier
                                                : -normal * offset * nextMultiplier);
                    }
                    else
                    {
                        subdivided[i * 2] = current;
                    }

                    if (setSecond)
                    {
                        subdivided[i * 2 + 1] = next + (secondRight
                            ? normal * offset * nextMultiplier
                            : -normal * offset * nextMultiplier);
                    }
                    else
                    {
                        subdivided[i * 2 + 1] = next;
                    }
                }

                subdivided[(path.Count - 2) * (int)Mathf.Pow(2, iteration + 1) + 2 - 1] =
                    subdivided2[currentPathLength - 1];
            }

            ListPool<Vector3>.Release(ref subdivided2);

            return subdivided;
        }

        /// <summary>Simple 平滑实现</summary>
        public List<Vector3> SmoothSimple(List<Vector3> path)
        {
            if (path.Count < 2) return path;

            List<Vector3> subdivided;

            if (uniformLength)
            {
                // Clamp to a small value to avoid the path being divided into a huge number of segments
                maxSegmentLength = Mathf.Max(maxSegmentLength, 0.005f);

                float pathLength = 0;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    pathLength += Vector3.Distance(path[i], path[i + 1]);
                }

                int estimatedNumberOfSegments = Mathf.FloorToInt(pathLength / maxSegmentLength);
                // Get a list with an initial capacity high enough so that we can add all points
                subdivided = ListPool<Vector3>.Claim(estimatedNumberOfSegments + 2);

                float distanceAlong = 0;

                // Sample points every [maxSegmentLength] world units along the path
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var start = path[i];
                    var end = path[i + 1];

                    float length = Vector3.Distance(start, end);

                    while (distanceAlong < length)
                    {
                        subdivided.Add(Vector3.Lerp(start, end, distanceAlong / length));
                        distanceAlong += maxSegmentLength;
                    }

                    distanceAlong -= length;
                }

                // Make sure we get the exact position of the last point
                subdivided.Add(path[path.Count - 1]);
            }
            else
            {
                subdivisions = Mathf.Max(subdivisions, 0);

                if (subdivisions > 10)
                {
                    Debug.LogWarning(
                        "Very large number of subdivisions. Cowardly refusing to subdivide every segment into more than " +
                        (1 << subdivisions) + " subsegments");
                    subdivisions = 10;
                }

                int steps = 1 << subdivisions;
                subdivided = ListPool<Vector3>.Claim((path.Count - 1) * steps + 1);
                Polygon.Subdivide(path, subdivided, steps);
            }

            if (strength > 0)
            {
                for (int it = 0; it < iterations; it++)
                {
                    Vector3 prev = subdivided[0];

                    for (int i = 1; i < subdivided.Count - 1; i++)
                    {
                        Vector3 tmp = subdivided[i];

                        // prev is at this point set to the value that subdivided[i-1] had before this loop started
                        // Move the point closer to the average of the adjacent points
                        subdivided[i] = Vector3.Lerp(tmp, (prev + subdivided[i + 1]) / 2F, strength);

                        prev = tmp;
                    }
                }
            }

            return subdivided;
        }

        /// <summary>Bezier 平滑实现</summary>
        public List<Vector3> SmoothBezier(List<Vector3> path)
        {
            if (subdivisions < 0) subdivisions = 0;

            int subMult = 1 << subdivisions;
            List<Vector3> subdivided = ListPool<Vector3>.Claim();

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 tangent1;
                Vector3 tangent2;
                if (i == 0)
                {
                    tangent1 = path[i + 1] - path[i];
                }
                else
                {
                    tangent1 = path[i + 1] - path[i - 1];
                }

                if (i == path.Count - 2)
                {
                    tangent2 = path[i] - path[i + 1];
                }
                else
                {
                    tangent2 = path[i] - path[i + 2];
                }

                tangent1 *= bezierTangentLength;
                tangent2 *= bezierTangentLength;

                Vector3 v1 = path[i];
                Vector3 v2 = v1 + tangent1;
                Vector3 v4 = path[i + 1];
                Vector3 v3 = v4 + tangent2;

                for (int j = 0; j < subMult; j++)
                {
                    subdivided.Add(AstarSplines.CubicBezier(v1, v2, v3, v4, (float)j / subMult));
                }
            }

            // Assign the last point
            subdivided.Add(path[path.Count - 1]);

            return subdivided;
        }
    }
}