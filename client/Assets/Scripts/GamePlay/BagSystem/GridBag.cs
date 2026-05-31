using System;
using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;
using Vector2Int = UnityEngine.Vector2Int;

/// <summary>
/// 2D 网格空间背包核心逻辑(纯逻辑,不依赖 MonoBehaviour,可单测)。
/// 模型:固定 <see cref="Width"/>×<see cref="Height"/> 网格,物品按 <see cref="ItemShape"/> 占一组格子
/// (支持 L/T 等不规则多边形),可 4 向旋转、可拖放/合并/交换;可叠加物品按 <see cref="MaxStack"/> 堆叠。
/// 用一维占位表 <c>_occ</c>(0=空,否则=实例 id)做 O(占格数) 的重叠检测。
/// 物品定义通过构造时传入的 resolver 查询(由 <see cref="BagSystem"/> 接 ConfigManager 提供),
/// 形状按 itemId 缓存。任意变化触发一次 <see cref="OnChanged"/>,桥接到 EventMgr.RefreshBagList。
///
/// 坐标:x=列(0..Width-1,向右),y=行(0..Height-1,向下),锚点为形状包围盒左上格。
/// </summary>
public class GridBag
{
    private readonly Func<int, Item> _resolve;
    private readonly List<PlacedItem> _items = new List<PlacedItem>();
    private readonly int[] _occ;
    private readonly Dictionary<int, ItemShape> _shapeCache = new Dictionary<int, ItemShape>();
    private int _nextInstanceId = 1;

    /// <summary>背包内容发生变化时触发(UI 据此整体刷新)。</summary>
    public event Action OnChanged;

    public int Width { get; }
    public int Height { get; }

    /// <summary>当前所有已放置物品(只读)。</summary>
    public IReadOnlyList<PlacedItem> Items => _items;

    public GridBag(int width, int height, Func<int, Item> resolve)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _occ = new int[Width * Height];
    }

    // ---------------- 形状 / 堆叠 ----------------

    /// <summary>取物品形状(按 itemId 缓存)。</summary>
    public ItemShape GetShape(int itemId)
    {
        if (_shapeCache.TryGetValue(itemId, out var s)) return s;
        var cfg = _resolve(itemId);
        int bw = cfg != null && cfg.Width > 0 ? cfg.Width : 1;
        int bh = cfg != null && cfg.Height > 0 ? cfg.Height : 1;
        string mask = cfg != null ? cfg.Shape : null;
        s = new ItemShape(bw, bh, mask);
        _shapeCache[itemId] = s;
        return s;
    }

    /// <summary>某物品的堆叠上限(&lt;=1 视为不可叠加)。</summary>
    public int MaxStack(int itemId)
    {
        var cfg = _resolve(itemId);
        return cfg != null && cfg.MaxStack > 1 ? cfg.MaxStack : 1;
    }

    /// <summary>某物品是否可叠加。</summary>
    public bool IsStackable(int itemId) => MaxStack(itemId) > 1;

    /// <summary>某物品某朝向的包围盒宽。</summary>
    public int EffW(int itemId, int rotation) => GetShape(itemId).WByRot[rotation & 3];
    /// <summary>某物品某朝向的包围盒高。</summary>
    public int EffH(int itemId, int rotation) => GetShape(itemId).HByRot[rotation & 3];
    /// <summary>某物品某朝向的本地占格集合(相对锚点)。
    /// 注意:返回的是内部缓存数组的引用,**只读**,调用方不得修改其元素(会污染所有同 itemId 物品的形状)。</summary>
    public Vector2Int[] LocalCells(int itemId, int rotation) => GetShape(itemId).CellsByRot[rotation & 3];

    private int Idx(int x, int y) => y * Width + x;

    // ---------------- 查询 ----------------

    public PlacedItem GetByInstance(int instanceId)
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].instanceId == instanceId) return _items[i];
        return null;
    }

    /// <summary>取占据格子 (x,y) 的物品,空则 null。</summary>
    public PlacedItem GetAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        int id = _occ[Idx(x, y)];
        return id == 0 ? null : GetByInstance(id);
    }

    /// <summary>统计某物品的总数量(累加各堆 count)。</summary>
    public int CountItem(int itemId)
    {
        int n = 0;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].itemId == itemId) n += _items[i].count;
        return n;
    }

    /// <summary>某物品(指定朝向、锚点)是否可放(每个占格都界内且空,ignore 实例视为空)。</summary>
    private bool FitsCells(int itemId, int ax, int ay, int rotation, int ignore)
    {
        var cells = LocalCells(itemId, rotation);
        for (int i = 0; i < cells.Length; i++)
        {
            int x = ax + cells[i].x;
            int y = ay + cells[i].y;
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            int id = _occ[Idx(x, y)];
            if (id != 0 && id != ignore) return false;
        }
        return true;
    }

    /// <summary>对外:某物品(指定朝向)能否放在锚点 (x,y)。ignoreInstance 用于移动/旋转时忽略自身。</summary>
    public bool CanPlace(int itemId, int x, int y, int rotation, int ignoreInstance = 0)
        => FitsCells(itemId, x, y, rotation, ignoreInstance);

    // ---------------- 增 / 删 / 拆 ----------------

    /// <summary>在指定锚点+朝向放入一个新物品实例(指定数量,夹到上限)。失败(越界/重叠/无配置)返回 null。</summary>
    public PlacedItem TryAddItemAt(int itemId, int x, int y, int rotation = 0, int count = 1)
    {
        if (_resolve(itemId) == null) return null;
        if (!FitsCells(itemId, x, y, rotation, 0)) return null;
        count = Math.Clamp(count, 1, MaxStack(itemId));
        var item = new PlacedItem(_nextInstanceId++, itemId, x, y, rotation, count);
        _items.Add(item);
        Stamp(item, item.instanceId);
        OnChanged?.Invoke();
        return item;
    }

    /// <summary>
    /// 放入若干个某物品:可叠加物品先并入已有未满堆叠,再为剩余开新堆叠(每堆不超过上限);
    /// 不可叠加物品逐个占格放置。返回**未能放入**的剩余数量(0 = 全部放入)。
    /// </summary>
    public int TryAddItem(int itemId, int count = 1)
    {
        if (_resolve(itemId) == null || count <= 0) return count;
        int max = MaxStack(itemId);
        int remaining = count;
        bool changed = false;

        // 1) 并入已有未满堆叠(仅可叠加物品)
        if (max > 1)
        {
            for (int i = 0; i < _items.Count && remaining > 0; i++)
            {
                var it = _items[i];
                if (it.itemId != itemId || it.count >= max) continue;
                int add = Math.Min(max - it.count, remaining);
                it.count += add;
                remaining -= add;
                changed = true;
            }
        }

        // 2) 为剩余开新堆叠(每堆最多 max),逐堆找空位
        while (remaining > 0)
        {
            int stack = Math.Min(max, remaining);
            var placed = PlaceFirstFit(itemId, stack);
            if (placed == null) break; // 没空位了
            remaining -= stack;
            changed = true;
        }

        if (changed) OnChanged?.Invoke();
        return remaining;
    }

    public bool RemoveItem(int instanceId)
    {
        var item = GetByInstance(instanceId);
        if (item == null) return false;
        Stamp(item, 0);
        _items.Remove(item);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 从某堆拆出 <paramref name="amount"/> 个,新建一堆放到首个空位(优先沿原朝向)。
    /// 要求物品可叠加、amount 在 [1, count-1] 内、且有空位。成功返回新实例,否则 null(原堆不变)。
    /// </summary>
    public PlacedItem SplitStack(int instanceId, int amount)
    {
        var item = GetByInstance(instanceId);
        if (item == null) return null;
        if (!IsStackable(item.itemId)) return null;
        if (amount < 1 || amount >= item.count) return null;

        var spot = FindFirstFit(item.itemId, item.rotation);
        if (spot != null)
        {
            var created = new PlacedItem(_nextInstanceId++, item.itemId, spot.Value.x, spot.Value.y, item.rotation, amount);
            _items.Add(created);
            Stamp(created, created.instanceId);
            item.count -= amount;
            OnChanged?.Invoke();
            return created;
        }

        // 原朝向无空位,退化到任意朝向找空位
        var fallback = PlaceFirstFit(item.itemId, amount);
        if (fallback == null) return null;
        item.count -= amount;
        OnChanged?.Invoke();
        return fallback;
    }

    public void Clear()
    {
        _items.Clear();
        Array.Clear(_occ, 0, _occ.Length);
        OnChanged?.Invoke();
    }

    // ---------------- 跨容器转移 ----------------

    /// <summary>对外触发一次变更事件(跨容器转移后,源容器手动刷新用)。</summary>
    public void RaiseChanged() => OnChanged?.Invoke();

    /// <summary>
    /// 把 <paramref name="from"/> 里的某实例转移到 <paramref name="to"/>(背包↔宝箱拖拽用)。
    /// 优先放到目标锚点 (x,y)+朝向;放不下则自动并堆/找空位(<see cref="TryAddItem"/>)。
    /// 成功后从源移除已转移数量;一个都放不下则源不变、返回 false(UI 贴回原位)。
    /// from==to 退化为同容器 <see cref="PlaceOrSwap"/>。
    /// </summary>
    public static bool Transfer(GridBag from, int instanceId, GridBag to, int x, int y, int rotation)
    {
        if (from == null || to == null) return false;
        if (from == to) return to.PlaceOrSwap(instanceId, x, y, rotation);

        var item = from.GetByInstance(instanceId);
        if (item == null) return false;
        int itemId = item.itemId, count = item.count;
        rotation &= 3;

        // 1) 先试目标锚点精确落位(整堆)
        if (to.CanPlace(itemId, x, y, rotation, 0) && to.TryAddItemAt(itemId, x, y, rotation, count) != null)
        {
            from.RemoveItem(instanceId);
            return true;
        }

        // 2) 退化:自动并堆 + 找空位
        int leftover = to.TryAddItem(itemId, count);
        if (leftover >= count) return false; // 一个都没进(目标满),源不变
        int moved = count - leftover;
        if (moved >= count) from.RemoveItem(instanceId);
        else { item.count -= moved; from.RaiseChanged(); } // 目标没全放下,源留剩余
        return true;
    }

    // ---------------- 移动 / 合并 / 交换 / 旋转 ----------------

    /// <summary>
    /// 放置/合并/交换(UI 拖放落点调用):把实例移到锚点 (x,y) + 指定朝向。
    ///   - 目标区域空 → 直接移动(锚点越界自动夹回界内)。
    ///   - 目标恰好只压住一个「同种可叠加且未满」的物品 → 合并(超出上限的部分留在原位)。
    ///   - 目标恰好只压住一个其它物品 → 快速交换(对方移到本实例原位;干净不重叠才成功)。
    ///   - 其它(压住多个 / 交换放不下)→ 失败,原位不动。
    /// </summary>
    public bool PlaceOrSwap(int instanceId, int x, int y, int rotation)
    {
        var item = GetByInstance(instanceId);
        if (item == null) return false;

        rotation &= 3;
        int ew = EffW(item.itemId, rotation);
        int eh = EffH(item.itemId, rotation);
        int ax = Math.Clamp(x, 0, Math.Max(0, Width - ew));
        int ay = Math.Clamp(y, 0, Math.Max(0, Height - eh));

        int oldX = item.x, oldY = item.y, oldRot = item.rotation;
        bool noChange = ax == oldX && ay == oldY && rotation == oldRot;

        Stamp(item, 0); // 先把自己从占位表抬走

        // 1) 直接放下(目标区域空)
        if (FitsCells(item.itemId, ax, ay, rotation, 0))
        {
            if (noChange) { Stamp(item, item.instanceId); return false; }
            item.x = ax; item.y = ay; item.rotation = rotation;
            Stamp(item, item.instanceId);
            OnChanged?.Invoke();
            return true;
        }

        // 2) 落在唯一其它实例上:先试合并,否则交换
        var other = FindSingleCovered(item.itemId, ax, ay, rotation);
        if (other != null)
        {
            // 2a) 合并:同一物品 + 可叠加 + 对方未满 + 不是自己
            int mergeMax = MaxStack(item.itemId);
            if (other.itemId == item.itemId && mergeMax > 1 && other.count < mergeMax && other.instanceId != item.instanceId)
            {
                int move = Math.Min(item.count, mergeMax - other.count);
                other.count += move;
                item.count -= move;
                if (item.count <= 0) _items.Remove(item); // A 全部并入 B(已 Stamp 0),移除 A
                else Stamp(item, item.instanceId);        // A 还有剩余,留原位
                OnChanged?.Invoke();
                return true;
            }

            // 2b) 快速交换
            Stamp(other, 0); // 把对方也抬走,网格此时不含 A、B

            // 先把 A 落到目标
            item.x = ax; item.y = ay; item.rotation = rotation;
            Stamp(item, item.instanceId);

            // 对方去 A 原位时,必须在「A 已就位」的网格上判定,否则拖动距离小于物品尺寸会重叠
            if (FitsCells(other.itemId, oldX, oldY, oldRot, 0))
            {
                other.x = oldX; other.y = oldY; other.rotation = oldRot;
                Stamp(other, other.instanceId);
                OnChanged?.Invoke();
                return true;
            }

            // 交换不干净:撤销 A 的临时落位,A、B 全部归位
            Stamp(item, 0);
            item.x = oldX; item.y = oldY; item.rotation = oldRot;
            Stamp(item, item.instanceId);
            Stamp(other, other.instanceId);
            return false;
        }

        // 失败:本实例归位
        Stamp(item, item.instanceId);
        return false;
    }

    /// <summary>旋转某实例 90°(顺时针)。优先原地;放不下时把锚点夹回界内再试;仍不行返回 false。</summary>
    public bool RotateItem(int instanceId)
    {
        var item = GetByInstance(instanceId);
        if (item == null) return false;

        int newRot = (item.rotation + 1) & 3;
        int ew = EffW(item.itemId, newRot);
        int eh = EffH(item.itemId, newRot);
        if (ew > Width || eh > Height) return false;

        int cx = Math.Clamp(item.x, 0, Width - ew);
        int cy = Math.Clamp(item.y, 0, Height - eh);

        Stamp(item, 0);
        if (FitsCells(item.itemId, cx, cy, newRot, 0))
        {
            item.x = cx; item.y = cy; item.rotation = newRot;
            Stamp(item, item.instanceId);
            OnChanged?.Invoke();
            return true;
        }
        Stamp(item, item.instanceId); // 归位
        return false;
    }

    // ---------------- 整理 ----------------

    /// <summary>
    /// 整理:先合并同种可叠加物品的零散堆叠,再按占格面积降序、逐个首适配(各朝向择优)放回,紧凑重排。
    /// </summary>
    public void SortBag()
    {
        // 1) 收集 (itemId, 总数);可叠加物品合并总数,不可叠加物品每个实例各记一份
        var stacks = new List<(int itemId, int count)>();
        var mergeIndex = new Dictionary<int, int>(); // 可叠加物品 itemId → stacks 下标
        foreach (var it in _items)
        {
            int max = MaxStack(it.itemId);
            if (max > 1 && mergeIndex.TryGetValue(it.itemId, out int idx))
            {
                stacks[idx] = (it.itemId, stacks[idx].count + it.count);
            }
            else
            {
                if (max > 1) mergeIndex[it.itemId] = stacks.Count;
                stacks.Add((it.itemId, it.count));
            }
        }

        // 2) 按面积降序 → SortPriority 降序 → id
        stacks.Sort((a, b) =>
        {
            int areaA = LocalCells(a.itemId, 0).Length;
            int areaB = LocalCells(b.itemId, 0).Length;
            if (areaB != areaA) return areaB.CompareTo(areaA);
            var ca = _resolve(a.itemId); var cb = _resolve(b.itemId);
            int pa = ca != null ? ca.SortPriority : 0;
            int pb = cb != null ? cb.SortPriority : 0;
            if (pb != pa) return pb.CompareTo(pa);
            return a.itemId.CompareTo(b.itemId);
        });

        // 3) 清空重放;合并后的总数若超过单堆上限,拆成多堆放置
        _items.Clear();
        Array.Clear(_occ, 0, _occ.Length);
        foreach (var s in stacks)
        {
            int max = MaxStack(s.itemId);
            int left = s.count;
            do
            {
                int stack = Math.Min(max, left);
                if (PlaceFirstFit(s.itemId, stack) == null) break; // 放不下,丢弃剩余(背包已满)
                left -= stack;
            } while (left > 0);
        }
        OnChanged?.Invoke();
    }

    // ---------------- 存档 ----------------

    public GridBagSaveData ToSaveData()
    {
        var data = new GridBagSaveData
        {
            width = Width, height = Height, nextInstanceId = _nextInstanceId,
            items = new List<PlacedItemSaveData>(_items.Count)
        };
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            data.items.Add(new PlacedItemSaveData
            {
                instanceId = it.instanceId, itemId = it.itemId, x = it.x, y = it.y, rotation = it.rotation, count = it.count
            });
        }
        return data;
    }

    public void LoadFromSaveData(GridBagSaveData data)
    {
        _items.Clear();
        Array.Clear(_occ, 0, _occ.Length);
        _nextInstanceId = 1;
        if (data?.items == null) { OnChanged?.Invoke(); return; }

        int maxId = 0;
        foreach (var s in data.items)
        {
            if (_resolve(s.itemId) == null) continue;             // 配表已删除
            if (!FitsCells(s.itemId, s.x, s.y, s.rotation, 0)) continue; // 位置非法
            int cnt = Math.Clamp(s.count <= 0 ? 1 : s.count, 1, MaxStack(s.itemId));
            var item = new PlacedItem(s.instanceId, s.itemId, s.x, s.y, s.rotation, cnt);
            _items.Add(item);
            Stamp(item, item.instanceId);
            if (s.instanceId > maxId) maxId = s.instanceId;
        }
        _nextInstanceId = Math.Max(data.nextInstanceId, maxId + 1);
        OnChanged?.Invoke();
    }

    // ---------------- 私有 ----------------

    /// <summary>在指定朝向下扫描首个能放下的锚点;无则 null。</summary>
    private Vector2Int? FindFirstFit(int itemId, int rotation)
    {
        int ew = EffW(itemId, rotation), eh = EffH(itemId, rotation);
        if (ew > Width || eh > Height) return null;
        for (int y = 0; y + eh <= Height; y++)
            for (int x = 0; x + ew <= Width; x++)
                if (FitsCells(itemId, x, y, rotation, 0))
                    return new Vector2Int(x, y);
        return null;
    }

    /// <summary>目标占格覆盖的「唯一」其它实例(覆盖 0 个或多于 1 个都返回 null)。</summary>
    private PlacedItem FindSingleCovered(int itemId, int ax, int ay, int rotation)
    {
        var cells = LocalCells(itemId, rotation);
        int foundId = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            int x = ax + cells[i].x, y = ay + cells[i].y;
            if (x < 0 || y < 0 || x >= Width || y >= Height) return null; // 越界不交换
            int id = _occ[Idx(x, y)];
            if (id == 0) continue;
            if (foundId == 0) foundId = id;
            else if (foundId != id) return null; // 多于一个
        }
        return foundId == 0 ? null : GetByInstance(foundId);
    }

    /// <summary>首适配放入(扫描 4 朝向 + 全部格位)指定数量的一堆,不触发 OnChanged。失败返回 null。</summary>
    private PlacedItem PlaceFirstFit(int itemId, int count = 1)
    {
        if (_resolve(itemId) == null) return null;
        for (int rot = 0; rot < 4; rot++)
        {
            var spot = FindFirstFit(itemId, rot);
            if (spot == null) continue;
            var item = new PlacedItem(_nextInstanceId++, itemId, spot.Value.x, spot.Value.y, rot, count);
            _items.Add(item);
            Stamp(item, item.instanceId);
            return item;
        }
        return null;
    }

    /// <summary>把物品占据的所有格写成 value(放置=instanceId,清除=0)。</summary>
    private void Stamp(PlacedItem item, int value)
    {
        var cells = LocalCells(item.itemId, item.rotation);
        for (int i = 0; i < cells.Length; i++)
        {
            int x = item.x + cells[i].x, y = item.y + cells[i].y;
            if (x >= 0 && y >= 0 && x < Width && y < Height) _occ[Idx(x, y)] = value;
        }
    }
}

/// <summary>网格背包存档结构。</summary>
[Serializable]
public class GridBagSaveData
{
    public int width;
    public int height;
    public int nextInstanceId;
    public List<PlacedItemSaveData> items;
}

/// <summary>单个已放置物品的存档结构(形状不存,读档时按配表重算;存朝向 + 数量)。</summary>
[Serializable]
public class PlacedItemSaveData
{
    public int instanceId;
    public int itemId;
    public int x;
    public int y;
    public int rotation;
    public int count;
}
