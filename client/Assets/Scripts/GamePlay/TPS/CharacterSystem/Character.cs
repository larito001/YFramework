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
    /// <summary>右键按住=瞄准=抬枪。AimComponent 写，view 喂 Animator IsAiming，WeaponComponent 用它门控 IsShooting。</summary>
    public bool IsAiming;
    /// <summary>归一化水平速度（horizontal speed / WalkSpeed，clamp[0,1]）。Run 1D BlendTree 用。</summary>
    public float AnimSpeedRatio;
    /// <summary>一次性 trigger：组件置 true，view 消费后清回 false</summary>
    public bool MeleeAttack;
    public int MeleeType;
    /// <summary>切枪一次性 trigger：WeaponComponent 切槽时置 true，view 消费 SetTrigger 后清回。</summary>
    public bool WeaponSwap;

    /// <summary>瞄准点世界坐标。AimComponent 写入，射击/UI 用它做命中检测、画准星等。</summary>
    public Vector3 AimTargetWorldPos;

    /// <summary>当前持有武器槽位。WeaponComponent 写入，业务/UI 读取。</summary>
    public int CurrentWeaponSlot;

    /// <summary>当前武器模型 Resources 路径。null/空 = 卸下武器。view 检测变化时挂/卸右手 socket。</summary>
    public string CurrentWeaponModelPath;
    public Vector3 CurrentWeaponLocalPosition;
    public Vector3 CurrentWeaponLocalEuler;

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
