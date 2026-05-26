using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏内的任意物体。ID 全局唯一，构造时自动分配。
/// </summary>
public class Actor
{
    private static int idCounter = 1;
    public int ID { get; } = idCounter++;
}
