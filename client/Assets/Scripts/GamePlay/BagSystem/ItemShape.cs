using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品的占格形状(支持不规则多边形,如 L/T 形)。由配表 <c>Item.Shape</c> 掩码解析而来:
/// 掩码按行用 '|' 分隔,字符 '1'=占据该格、'0'=空;留空则按 Width×Height 填满成矩形。
/// 例 "10|11" = L 形(2×2 去掉右上角)。
///
/// 预计算 4 个旋转朝向(0/90/180/270 顺时针)的本地占格集合与包围盒尺寸,运行时 O(1) 取用。
/// 旋转公式(顺时针 90°,在 w×h 网格内):(x,y) → (h-1-y, x),新包围盒变为 h×w。
/// 本地坐标系:x 向右、y 向下,锚点(0,0)在左上;旋转后已归一化到 [0,w')×[0,h')。
/// </summary>
public sealed class ItemShape
{
    /// <summary>按旋转朝向(0..3)缓存的本地占格集合。</summary>
    public readonly Vector2Int[][] CellsByRot = new Vector2Int[4][];
    /// <summary>每个朝向的包围盒宽。</summary>
    public readonly int[] WByRot = new int[4];
    /// <summary>每个朝向的包围盒高。</summary>
    public readonly int[] HByRot = new int[4];

    public ItemShape(int baseW, int baseH, string mask)
    {
        var cells = ParseBase(baseW, baseH, mask, out int w, out int h);
        int cw = w, ch = h;
        for (int r = 0; r < 4; r++)
        {
            CellsByRot[r] = cells.ToArray();
            WByRot[r] = cw;
            HByRot[r] = ch;

            // 顺时针 90° 旋转得到下一朝向:(x,y) -> (h-1-y, x),包围盒 w/h 互换
            var next = new List<Vector2Int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                next.Add(new Vector2Int(ch - 1 - c.y, c.x));
            }
            cells = next;
            (cw, ch) = (ch, cw);
        }
    }

    private static List<Vector2Int> ParseBase(int baseW, int baseH, string mask, out int w, out int h)
    {
        var list = new List<Vector2Int>();

        if (!string.IsNullOrEmpty(mask))
        {
            var rows = mask.Split('|');
            h = rows.Length;
            w = 0;
            foreach (var row in rows)
                if (row.Length > w) w = row.Length;

            for (int y = 0; y < h; y++)
            {
                var row = rows[y];
                for (int x = 0; x < row.Length; x++)
                    if (row[x] == '1') list.Add(new Vector2Int(x, y));
            }

            if (list.Count > 0)
            {
                // 校验:掩码包围盒应与配表 width×height 一致,不符多半是配表填错(数据仍以掩码为准)
                if ((baseW > 0 && baseW != w) || (baseH > 0 && baseH != h))
                    Debug.LogWarning($"[ItemShape] 形状掩码尺寸 {w}x{h} 与配表 width×height({baseW}x{baseH})不一致,以掩码为准。mask=\"{mask}\"");
                return list;
            }
            // 掩码全 0 → 回退矩形
            Debug.LogWarning($"[ItemShape] 形状掩码全为 0,回退为 {Mathf.Max(1, baseW)}x{Mathf.Max(1, baseH)} 矩形。mask=\"{mask}\"");
        }

        // 无掩码 / 非法掩码:填满 baseW×baseH 矩形
        w = Mathf.Max(1, baseW);
        h = Mathf.Max(1, baseH);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                list.Add(new Vector2Int(x, y));
        return list;
    }
}
