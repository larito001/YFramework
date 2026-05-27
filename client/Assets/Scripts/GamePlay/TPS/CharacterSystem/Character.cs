using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有动态物体。纯数据 + 组件容器，不持有 view 引用。
///
/// 数据流：
///   组件 Tick 写 "意图" 字段（WishVelocity / Rotation / AnimMoveX..）
///   CharacterView.LateUpdate 读意图驱动 CC / Transform / Animator
///   view 物理跑完后回写 "状态" 字段（Position / IsGrounded）
/// 组件下一帧基于回写的状态继续算。view 完全被动，Character 不知道它存在。
/// </summary>
public class Character : Actor
{
    // ── 状态（view 物理后回写，组件读取） ──
    public Vector3 Position;
    public bool IsGrounded;

    // ── 意图（组件写，view 读取/应用） ──
    public Quaternion Rotation = Quaternion.identity;
    public Vector3 WishVelocity;
    public float AnimMoveX;
    public float AnimMoveY;
    public bool IsShooting;
    /// <summary>一次性 trigger：组件置 true，view 消费后清回 false</summary>
    public bool MeleeAttack;
    public int MeleeType;

    private readonly List<ICharacterComponent> components = new List<ICharacterComponent>();

    public T Add<T>(T comp) where T : ICharacterComponent
    {
        components.Add(comp);
        comp.Attach(this);
        return comp;
    }

    public T Get<T>() where T : ICharacterComponent
    {
        for (int i = 0; i < components.Count; i++)
            if (components[i] is T t) return t;
        return null;
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < components.Count; i++)
            components[i].Tick(dt);
    }

    public void Dispose()
    {
        for (int i = components.Count - 1; i >= 0; i--)
            components[i].Detach();
        components.Clear();
    }
}
