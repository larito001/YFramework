using System;
using System.Collections.Generic;
using YFramework.Config;

/// <summary>
/// 背包容器核心逻辑（纯 C#，不依赖 Unity，方便单测）。采用固定槽位模型：每格 null 表示空，
/// 否则持有一个 <see cref="ItemStack"/>。提供加入/移除/移动/拆分/排序/扩容等成熟背包操作。
/// 物品定义通过构造时传入的 resolver 委托查询（由 <see cref="BagSystem"/> 接 ConfigManager 的 item 配表提供，
/// 解耦数据来源——数据全部走打表工具生成的 protobuf，不用 ScriptableObject）。
/// 任意变化触发一次 <see cref="OnChanged"/>，由 <see cref="BagSystem"/> 桥接到 EventMgr.RefreshBagList。
/// </summary>
public class Bag
{
    private readonly Func<int, Item> _resolve;
    private readonly List<ItemStack> _slots;

    /// <summary>背包内容发生变化时触发（UI 据此刷新）。</summary>
    public event Action OnChanged;

    public Bag(int capacity, Func<int, Item> resolve)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        if (capacity < 1) capacity = 1;
        _slots = new List<ItemStack>(capacity);
        for (int i = 0; i < capacity; i++) _slots.Add(null);
    }

    /// <summary>槽位总数。</summary>
    public int Capacity => _slots.Count;

    /// <summary>当前已占用的槽位数。</summary>
    public int UsedSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) n++;
            return n;
        }
    }

    /// <summary>是否所有槽位都被占满（不考虑可堆叠剩余空间）。</summary>
    public bool IsFull => UsedSlots >= Capacity;

    /// <summary>只读获取某槽位内容（可能为 null）。</summary>
    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return null;
        return _slots[index];
    }

    /// <summary>配表 MaxStack 容错：&lt;=0 视为 1（不可堆叠）。</summary>
    private static int MaxStackOf(Item cfg) => cfg != null && cfg.MaxStack > 0 ? cfg.MaxStack : 1;

    // ---------------- 查询 ----------------

    /// <summary>统计背包中某物品的总数量。</summary>
    public int GetItemCount(int itemId)
    {
        if (itemId <= 0) return 0;
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s != null && s.itemId == itemId) total += s.count;
        }
        return total;
    }

    /// <summary>背包是否拥有至少 count 个某物品。</summary>
    public bool HasItem(int itemId, int count = 1) => GetItemCount(itemId) >= count;

    // ---------------- 加入 ----------------

    /// <summary>
    /// 加入物品：优先堆叠到已有同种槽位，再放入空槽位。返回未能放入的剩余数量（0 表示全部成功）。
    /// </summary>
    public int AddItem(int itemId, int count)
    {
        var cfg = _resolve(itemId);
        if (cfg == null || count <= 0) return count;

        int remaining = count;
        int maxStack = MaxStackOf(cfg);

        // 1) 堆叠进已有槽位
        if (maxStack > 1)
        {
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s == null || s.itemId != itemId || s.count >= maxStack) continue;
                int add = Math.Min(maxStack - s.count, remaining);
                s.count += add;
                remaining -= add;
            }
        }

        // 2) 放入空槽位
        while (remaining > 0)
        {
            int empty = FindEmptySlot();
            if (empty < 0) break; // 背包满了
            int add = Math.Min(maxStack, remaining);
            _slots[empty] = new ItemStack(itemId, add);
            remaining -= add;
        }

        if (remaining != count) OnChanged?.Invoke();
        return remaining;
    }

    // ---------------- 移除 ----------------

    /// <summary>按物品 id 移除指定数量（可跨多个槽位）。返回实际移除的数量。</summary>
    public int RemoveItem(int itemId, int count)
    {
        if (itemId <= 0 || count <= 0) return 0;
        int toRemove = count;
        for (int i = 0; i < _slots.Count && toRemove > 0; i++)
        {
            var s = _slots[i];
            if (s == null || s.itemId != itemId) continue;
            int take = Math.Min(s.count, toRemove);
            s.count -= take;
            toRemove -= take;
            if (s.count <= 0) _slots[i] = null;
        }
        int removed = count - toRemove;
        if (removed > 0) OnChanged?.Invoke();
        return removed;
    }

    /// <summary>从指定槽位移除数量，返回实际移除量。</summary>
    public int RemoveAt(int index, int count)
    {
        if (index < 0 || index >= _slots.Count || count <= 0) return 0;
        var s = _slots[index];
        if (s == null) return 0;
        int take = Math.Min(s.count, count);
        s.count -= take;
        if (s.count <= 0) _slots[index] = null;
        if (take > 0) OnChanged?.Invoke();
        return take;
    }

    // ---------------- 移动 / 拆分 ----------------

    /// <summary>
    /// 移动槽位内容：目标为空则放入；同种可堆叠则合并（超出上限的部分留在原位）；否则交换。
    /// </summary>
    public bool MoveItem(int from, int to)
    {
        if (from == to) return false;
        if (from < 0 || from >= _slots.Count || to < 0 || to >= _slots.Count) return false;

        var src = _slots[from];
        if (src == null) return false;
        var dst = _slots[to];

        if (dst == null)
        {
            _slots[to] = src;
            _slots[from] = null;
        }
        else if (dst.itemId == src.itemId)
        {
            int maxStack = MaxStackOf(_resolve(src.itemId));
            int space = maxStack - dst.count;
            if (space <= 0)
            {
                Swap(from, to);
            }
            else
            {
                int move = Math.Min(space, src.count);
                dst.count += move;
                src.count -= move;
                if (src.count <= 0) _slots[from] = null;
            }
        }
        else
        {
            Swap(from, to);
        }

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>把某槽位拆出 amount 个到指定空槽位（targetSlot 为 -1 时自动寻找空位）。</summary>
    public bool SplitStack(int from, int amount, int targetSlot = -1)
    {
        if (from < 0 || from >= _slots.Count || amount <= 0) return false;
        var src = _slots[from];
        if (src == null || amount >= src.count) return false;

        int target = targetSlot >= 0 ? targetSlot : FindEmptySlot();
        if (target < 0 || target >= _slots.Count || _slots[target] != null) return false;

        src.count -= amount;
        _slots[target] = new ItemStack(src.itemId, amount);
        OnChanged?.Invoke();
        return true;
    }

    // ---------------- 整理 / 扩容 ----------------

    /// <summary>整理背包：合并同种堆叠，并按 类型(Type) → 排序权重(SortPriority, 降序) → id 排序后紧凑排列。</summary>
    public void SortBag()
    {
        var totals = new Dictionary<int, int>();
        var order = new List<int>();
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s == null) continue;
            if (!totals.ContainsKey(s.itemId)) { totals[s.itemId] = 0; order.Add(s.itemId); }
            totals[s.itemId] += s.count;
            _slots[i] = null;
        }

        order.Sort((a, b) =>
        {
            var ca = _resolve(a);
            var cb = _resolve(b);
            if (ca == null || cb == null) return a.CompareTo(b);
            int t = ca.Type.CompareTo(cb.Type);
            if (t != 0) return t;
            int p = cb.SortPriority.CompareTo(ca.SortPriority); // 权重降序
            if (p != 0) return p;
            return a.CompareTo(b);
        });

        int slot = 0;
        foreach (var id in order)
        {
            int maxStack = MaxStackOf(_resolve(id));
            int left = totals[id];
            while (left > 0 && slot < _slots.Count)
            {
                int add = Math.Min(maxStack, left);
                _slots[slot] = new ItemStack(id, add);
                left -= add;
                slot++;
            }
        }

        OnChanged?.Invoke();
    }

    /// <summary>扩容（只增不减），新增空槽位。</summary>
    public void Expand(int extraSlots)
    {
        if (extraSlots <= 0) return;
        for (int i = 0; i < extraSlots; i++) _slots.Add(null);
        OnChanged?.Invoke();
    }

    /// <summary>清空背包。</summary>
    public void Clear()
    {
        for (int i = 0; i < _slots.Count; i++) _slots[i] = null;
        OnChanged?.Invoke();
    }

    // ---------------- 存档 ----------------

    /// <summary>导出存档快照（只记录非空槽位）。</summary>
    public BagSaveData ToSaveData()
    {
        var data = new BagSaveData { capacity = Capacity, slots = new List<SlotSaveData>() };
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s == null || s.IsEmpty) continue;
            data.slots.Add(new SlotSaveData { index = i, itemId = s.itemId, count = s.count });
        }
        return data;
    }

    /// <summary>从存档快照恢复（重建容量并覆盖现有内容；丢弃配表已删除的物品）。</summary>
    public void LoadFromSaveData(BagSaveData data)
    {
        if (data == null) return;
        _slots.Clear();
        int cap = Math.Max(1, data.capacity);
        for (int i = 0; i < cap; i++) _slots.Add(null);
        if (data.slots != null)
        {
            foreach (var slot in data.slots)
            {
                if (slot == null || slot.index < 0 || slot.index >= _slots.Count) continue;
                if (_resolve(slot.itemId) == null) continue; // 配表已删除该物品，丢弃
                _slots[slot.index] = new ItemStack(slot.itemId, slot.count);
            }
        }
        OnChanged?.Invoke();
    }

    // ---------------- 私有工具 ----------------

    private int FindEmptySlot()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] == null) return i;
        return -1;
    }

    private void Swap(int a, int b)
    {
        (_slots[a], _slots[b]) = (_slots[b], _slots[a]);
    }
}

/// <summary>背包存档结构。</summary>
[Serializable]
public class BagSaveData
{
    public int capacity;
    public List<SlotSaveData> slots;
}

/// <summary>单个槽位的存档结构。</summary>
[Serializable]
public class SlotSaveData
{
    public int index;
    public int itemId;
    public int count;
}
