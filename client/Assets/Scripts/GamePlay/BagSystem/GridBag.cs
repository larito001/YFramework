using System;
using System.Collections.Generic;
using YFramework.Config;

/// <summary>
/// 2D 网格空间背包核心逻辑(纯 C#,不依赖 Unity,可单测)。
/// 模型:固定 <see cref="Width"/>×<see cref="Height"/> 网格,物品按配表宽高占一片矩形区域,**不堆叠**,
/// 可任意拖放、可 90° 旋转。用一维占位表 <c>occ</c>(0=空,否则=实例 id)做 O(面积) 的重叠检测。
/// 物品定义通过构造时传入的 resolver 查询(由 <see cref="BagSystem"/> 接 ConfigManager 提供)。
/// 任意变化触发一次 <see cref="OnChanged"/>,由 <see cref="BagSystem"/> 桥接到 EventMgr.RefreshBagList。
///
/// 坐标约定:x=列(从左,0..Width-1),y=行(从上,0..Height-1),锚点为物品左上角格。
/// </summary>
public class GridBag
{
    private readonly Func<int, Item> _resolve;
    private readonly List<PlacedItem> _items = new List<PlacedItem>();
    private readonly int[] _occ;            // Width*Height,0=空,否则=占据该格的实例 id
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

    /// <summary>从配表取物品原始占格尺寸(容错:&lt;1 视为 1)。</summary>
    private (int w, int h) BaseSizeOf(Item cfg)
    {
        int w = cfg != null && cfg.Width > 0 ? cfg.Width : 1;
        int h = cfg != null && cfg.Height > 0 ? cfg.Height : 1;
        return (w, h);
    }

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

    /// <summary>统计某 itemId 的实例个数(空间背包不堆叠,关心"有几个")。</summary>
    public int CountItem(int itemId)
    {
        int n = 0;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].itemId == itemId) n++;
        return n;
    }

    /// <summary>矩形 [x,x+w)×[y,y+h) 是否可放(界内且不与他人重叠,忽略 ignoreInstance 自身)。</summary>
    private bool Fits(int x, int y, int w, int h, int ignoreInstance)
    {
        if (x < 0 || y < 0 || x + w > Width || y + h > Height) return false;
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
            {
                int id = _occ[Idx(xx, yy)];
                if (id != 0 && id != ignoreInstance) return false;
            }
        return true;
    }

    /// <summary>某物品(指定朝向)能否放在锚点 (x,y)(ignoreInstance 用于移动/旋转时忽略自身)。</summary>
    public bool CanPlace(int itemId, int x, int y, bool rotated, int ignoreInstance = 0)
    {
        var cfg = _resolve(itemId);
        if (cfg == null) return false;
        var (bw, bh) = BaseSizeOf(cfg);
        int w = rotated ? bh : bw;
        int h = rotated ? bw : bh;
        return Fits(x, y, w, h, ignoreInstance);
    }

    // ---------------- 增 / 删 / 移动 / 旋转 ----------------

    /// <summary>在指定锚点+朝向放入一个新物品实例。失败(越界/重叠/无配置)返回 null。</summary>
    public PlacedItem TryAddItemAt(int itemId, int x, int y, bool rotated = false)
    {
        var cfg = _resolve(itemId);
        if (cfg == null) return null;
        var (bw, bh) = BaseSizeOf(cfg);
        int w = rotated ? bh : bw;
        int h = rotated ? bw : bh;
        if (!Fits(x, y, w, h, 0)) return null;

        var item = new PlacedItem(_nextInstanceId++, itemId, x, y, bw, bh, rotated);
        _items.Add(item);
        Stamp(item, item.instanceId);
        OnChanged?.Invoke();
        return item;
    }

    /// <summary>自动找第一个能放下的位置放入新物品:先试原朝向,再试旋转。背包无空位返回 null。</summary>
    public PlacedItem TryAddItem(int itemId)
    {
        var item = PlaceFirstFit(itemId);
        if (item != null) OnChanged?.Invoke();
        return item;
    }

    /// <summary>把某实例移动到新锚点(朝向不变)。越界/与他人重叠则失败(原位不动)。</summary>
    public bool MoveItem(int instanceId, int x, int y)
    {
        var item = GetByInstance(instanceId);
        if (item == null) return false;
        if (item.x == x && item.y == y) return false;
        if (!Fits(x, y, item.W, item.H, instanceId)) return false;

        Stamp(item, 0);
        item.x = x; item.y = y;
        Stamp(item, instanceId);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>原地旋转某实例 90°(锚点不变)。旋转后越界/重叠则失败(朝向不变)。</summary>
    public bool RotateItem(int instanceId)
    {
        var item = GetByInstance(instanceId);
        if (item == null) return false;
        if (item.baseW == item.baseH) return false; // 正方形旋转无意义

        int newW = item.H; // 旋转后 = 当前 H/W 互换
        int newH = item.W;
        if (!Fits(item.x, item.y, newW, newH, instanceId)) return false;

        Stamp(item, 0);
        item.rotated = !item.rotated;
        Stamp(item, instanceId);
        OnChanged?.Invoke();
        return true;
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

    public void Clear()
    {
        _items.Clear();
        Array.Clear(_occ, 0, _occ.Length);
        OnChanged?.Invoke();
    }

    // ---------------- 整理(自动旋转 + 紧凑重排)----------------

    /// <summary>
    /// 整理:取出所有物品,按占格面积降序、逐个首适配放回(每个物品先试原朝向再试旋转)。
    /// 大件优先放更紧凑,旋转择优能塞下更多。
    /// </summary>
    public void SortBag()
    {
        var ids = new List<int>(_items.Count);
        foreach (var it in _items) ids.Add(it.itemId);

        ids.Sort((a, b) =>
        {
            var ca = _resolve(a);
            var cb = _resolve(b);
            int areaA = ca != null ? Math.Max(1, ca.Width) * Math.Max(1, ca.Height) : 1;
            int areaB = cb != null ? Math.Max(1, cb.Width) * Math.Max(1, cb.Height) : 1;
            if (areaB != areaA) return areaB.CompareTo(areaA);
            // 面积相同按 SortPriority 降序,再按 id
            int pa = ca != null ? ca.SortPriority : 0;
            int pb = cb != null ? cb.SortPriority : 0;
            if (pb != pa) return pb.CompareTo(pa);
            return a.CompareTo(b);
        });

        _items.Clear();
        Array.Clear(_occ, 0, _occ.Length);
        foreach (var id in ids) PlaceFirstFit(id);
        OnChanged?.Invoke();
    }

    // ---------------- 存档 ----------------

    public GridBagSaveData ToSaveData()
    {
        var data = new GridBagSaveData
        {
            width = Width,
            height = Height,
            nextInstanceId = _nextInstanceId,
            items = new List<PlacedItemSaveData>(_items.Count)
        };
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            data.items.Add(new PlacedItemSaveData
            {
                instanceId = it.instanceId, itemId = it.itemId, x = it.x, y = it.y, rotated = it.rotated
            });
        }
        return data;
    }

    /// <summary>从存档恢复(覆盖现有内容;丢弃配表已删除或放不下的物品;宽高以配表为准重算)。</summary>
    public void LoadFromSaveData(GridBagSaveData data)
    {
        _items.Clear();
        Array.Clear(_occ, 0, _occ.Length);
        _nextInstanceId = 1;
        if (data?.items == null) { OnChanged?.Invoke(); return; }

        int maxId = 0;
        foreach (var s in data.items)
        {
            var cfg = _resolve(s.itemId);
            if (cfg == null) continue;
            var (bw, bh) = BaseSizeOf(cfg);
            int w = s.rotated ? bh : bw;
            int h = s.rotated ? bw : bh;
            if (!Fits(s.x, s.y, w, h, 0)) continue;
            var item = new PlacedItem(s.instanceId, s.itemId, s.x, s.y, bw, bh, s.rotated);
            _items.Add(item);
            Stamp(item, item.instanceId);
            if (s.instanceId > maxId) maxId = s.instanceId;
        }
        _nextInstanceId = Math.Max(data.nextInstanceId, maxId + 1);
        OnChanged?.Invoke();
    }

    // ---------------- 私有 ----------------

    /// <summary>首适配放入(先原朝向后旋转),不触发 OnChanged。失败返回 null。</summary>
    private PlacedItem PlaceFirstFit(int itemId)
    {
        var cfg = _resolve(itemId);
        if (cfg == null) return null;
        var (bw, bh) = BaseSizeOf(cfg);

        // 两种朝向都试:rotated=false 优先
        for (int r = 0; r < 2; r++)
        {
            bool rotated = r == 1;
            if (rotated && bw == bh) break; // 正方形无需第二朝向
            int w = rotated ? bh : bw;
            int h = rotated ? bw : bh;
            for (int y = 0; y + h <= Height; y++)
                for (int x = 0; x + w <= Width; x++)
                {
                    if (!Fits(x, y, w, h, 0)) continue;
                    var item = new PlacedItem(_nextInstanceId++, itemId, x, y, bw, bh, rotated);
                    _items.Add(item);
                    Stamp(item, item.instanceId);
                    return item;
                }
        }
        return null;
    }

    /// <summary>把物品占据的所有格写成 value(放置=instanceId,清除=0)。</summary>
    private void Stamp(PlacedItem item, int value)
    {
        for (int yy = item.y; yy < item.y + item.H; yy++)
            for (int xx = item.x; xx < item.x + item.W; xx++)
                _occ[Idx(xx, yy)] = value;
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

/// <summary>单个已放置物品的存档结构(宽高不存,读档时按配表重算;只存朝向)。</summary>
[Serializable]
public class PlacedItemSaveData
{
    public int instanceId;
    public int itemId;
    public int x;
    public int y;
    public bool rotated;
}
