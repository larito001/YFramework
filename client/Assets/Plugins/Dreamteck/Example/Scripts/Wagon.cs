namespace Dreamteck.Splines.Examples
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

// 车厢类，用于与 TrainEngine 协同工作，在样条(Spline)轨道上保持车厢之间的连接与偏移
    public class Wagon : MonoBehaviour
    {
        // 内部辅助类：代表一段样条（Spline）及其起点与终点信息
        // 如果 start 或 end 为 -1，则表示在该方向上没有限制（可以自由行进）
        public class SplineSegment
        {
            public SplineComputer spline; // 当前所在的样条
            public int start = -1, end = -1; // 样条的起点和终点索引
            public Spline.Direction direction; // 当前段的行进方向（前进或后退）

            // 通过样条、入口点和方向来创建新的样条段
            public SplineSegment(SplineComputer spline, int entryPoint, Spline.Direction direction)
            {
                this.spline = spline;
                start = entryPoint;
                this.direction = direction;
            }

            // 拷贝构造函数，用于复制一个现有的样条段
            public SplineSegment(SplineSegment input)
            {
                spline = input.spline;
                start = input.start;
                end = input.end;
                direction = input.direction;
            }

            // 根据当前百分比 (percent) 沿样条行进指定距离 (distance)
            // 如果启用了 loop（循环），就允许在封闭样条上循环移动
            public double Travel(double percent, float distance, Spline.Direction direction, out float moved, bool loop)
            {
                double max = direction == Spline.Direction.Forward ? 1.0 : 0.0; // 计算能够到达的最大百分比
                if (start >= 0) max = spline.GetPointPercent(start); // 如果定义了起点，使用起点百分比作为限制
                return TravelClamped(percent, distance, direction, max, out moved, loop); // 调用受限版本
            }

            // 沿着当前 segment 的“出口端”开始向前行进指定距离
            public double Travel(float distance, Spline.Direction direction, out float moved, bool loop)
            {
                double startPercent = spline.GetPointPercent(end); // 从终点开始
                double max = direction == Spline.Direction.Forward ? 1.0 : 0.0;
                if (start >= 0) max = spline.GetPointPercent(start);
                return TravelClamped(startPercent, distance, direction, max, out moved, loop);
            }

            // 实际行进实现：保证不会超越最大百分比 max，支持封闭样条的循环移动
            double TravelClamped(double percent, float distance, Spline.Direction direction, double max, out float moved, bool loop)
            {
                moved = 0f; // 累计移动距离
                float traveled = 0f;
                double result = spline.Travel(percent, distance, out traveled, direction); // 初次移动
                moved += traveled;
                // 如果允许循环但没走完全部距离，则在封闭样条上继续循环
                if (loop && moved < distance) {
                    if (direction == Spline.Direction.Forward && Mathf.Approximately((float)result, 1f))
                    {
                        // 当前到达结尾，从头再走剩余部分
                        result = spline.Travel(0.0, distance - moved, out traveled, direction);
                    } else if (direction == Spline.Direction.Backward && Mathf.Approximately((float)result, 0f))
                    {
                        // 当前到达起点，从末端再走剩余部分
                        result = spline.Travel(1.0, distance - moved, out traveled, direction);
                    }
                    moved += traveled;
                }

                // 限制不超过最大百分比 max
                if (direction == Spline.Direction.Forward && percent <= max)
                {
                    if (result > max)
                    {
                        moved -= spline.CalculateLength(result, max); // 减去超过部分
                        result = max;
                    }
                }
                else if (direction == Spline.Direction.Backward && percent >= max)
                {
                    if (result < max)
                    {
                        moved -= spline.CalculateLength(max, result);
                        result = max;
                    }
                }
                return result; // 返回最终百分比位置
            }
        }

        SplineTracer tracer; // 当前车厢的样条追踪器组件
        public bool isEngine = false; // 是否为火车头（由 TrainEngine 标识）
        public Wagon back; // 引用后方的车厢
        public float offset = 0f; // 当前车厢与前车厢之间的距离偏移
        Wagon front; // 前方车厢引用
        SplineSegment segment, tempSegment; // 当前样条段信息（以及临时变量）

        private void Awake()
        {
            tracer = GetComponent<SplineTracer>();
            // 如果此车厢是引擎，则递归设置整列车的层级关系与样条段信息
            if (isEngine) SetupRecursively(null, new SplineSegment(tracer.spline, -1, tracer.direction));
        }

        // 递归设置每节车厢的前后关系与样条连接信息
        void SetupRecursively(Wagon frontWagon, SplineSegment inputSegment)
        {
            front = frontWagon; // 设置前车厢引用
            segment = inputSegment; // 保存当前所在样条段
            if (back != null) back.SetupRecursively(this, segment); // 如果有后车厢，则继续递归初始化
        }

        // 更新车厢偏移量（包括所有后续车厢）
        public void UpdateOffset()
        {
            ApplyOffset(); // 自己先更新
            if (back != null) back.UpdateOffset(); // 递归更新下一节车厢
        }

        // 找出最前端的车厢（通常就是火车头）
        Wagon GetRootWagon()
        {
            Wagon current = this;
            while (current.front != null) current = current.front;
            return current;
        }

        // 实际执行位置偏移的函数
        void ApplyOffset()
        {
            if (isEngine)
            {
                // 如果是引擎，检查所有车厢是否在相同样条上
                ResetSegments();
                return;
            }
            float totalMoved = 0f, moved = 0f;
            double start = front.tracer.UnclipPercent(front.tracer.result.percent); // 获取前车厢的当前曲线位置
            // 沿着前车厢样条反向移动 offset 距离（也就是向后走 offset）
            Spline.Direction inverseDirection = front.segment.direction;
            InvertDirection(ref inverseDirection);
            SplineComputer spline = front.segment.spline;
            double percent = front.segment.Travel(start, offset, inverseDirection, out moved, front.segment.spline.isClosed);
            totalMoved += moved;
            // 如果正好走完 offset 距离（未遇到样条末端或节点）
            if (Mathf.Approximately(totalMoved, offset))
            {
                // 若 segment 不同步（车厢在不同样条上），强制同步
                if (segment != front.segment)
                {
                    if (back != null) back.segment = segment;
                }
                if(segment != front.segment) segment = front.segment;
                ApplyTracer(spline, percent, front.tracer.direction); // 让车厢追踪当前位置
                return;
            }

            // 如果没走够 offset（比如跨越了节点或切换样条）
            if (segment != front.segment)
            {
                inverseDirection = segment.direction;
                InvertDirection(ref inverseDirection);
                spline = segment.spline;
                percent = segment.Travel(offset - totalMoved, inverseDirection, out moved, segment.spline.isClosed);
                totalMoved += moved;
            }

            // 设置追踪器在新样条上的位置
            ApplyTracer(spline, percent, segment.direction);
        }

        // 检查所有车厢是否在同一 SplineSegment 上，如果是，则取消样条入口限制（允许自由循环）
        void ResetSegments()
        {
            Wagon current = back;
            bool same = true;
            while (current != null)
            {
                if(current.segment != segment)
                {
                    same = false;
                    break;
                }
                current = current.back;
            }

            // 如果所有车厢都在同样的样条段上，则清除 start 限制，使之可以循环
            if(same) segment.start = -1; 
        }

        // 更改车厢的样条、方向和位置百分比
        void ApplyTracer(SplineComputer spline, double percent, Spline.Direction direction)
        {
            bool rebuild = tracer.spline != spline; // 判断是否切换了样条
            tracer.spline = spline; // 更新样条
            if (rebuild) tracer.RebuildImmediate(); // 若样条变了，立即重建内部数据
            tracer.direction = direction; // 设置方向
            tracer.SetPercent(tracer.ClipPercent(percent)); // 设置百分比位置（已裁剪）
        }

        // 当火车进入新的样条段时调用，通知后方车厢同步
        public void EnterSplineSegment(int previousSplineExitPoint, SplineComputer spline, int entryPoint, Spline.Direction direction)
        {
            if (!isEngine) return; // 仅火车头执行此逻辑
            if (back != null)
            {
                segment.end = previousSplineExitPoint; // 记录当前段的出口点
                back.segment = segment; // 将此段传播到后车厢
            }

            // 创建新样条段：进入新的轨道
            segment = new SplineSegment(spline, entryPoint, direction);
        }

        // 反转方向（Forward <-> Backward）
        static void InvertDirection(ref Spline.Direction direction)
        {
            if (direction == Spline.Direction.Forward) direction = Spline.Direction.Backward;
            else direction = Spline.Direction.Forward;
        }
    }
}
