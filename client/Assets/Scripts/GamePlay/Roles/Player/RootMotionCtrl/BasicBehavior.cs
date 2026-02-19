using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移动、重力、旋转、地面检测
/// </summary>
public class BasicBehavior : ITickable,IAnimatorIK
{
    private MoveBehavior _moveBehavior;
    private AimBehavior _aimBehavior;
    List<IPlayerBehavior> _behaviors = new List<IPlayerBehavior>();
    List<ITickable> _tickables = new List<ITickable>();
    List<IAnimatorIK> _animatorIks = new List<IAnimatorIK>();
    public Animator Anim { get; private set; }
    public Transform Trans { get; private set; }
    public Camera MainCamera { get; private set; }
    public LayerMask AimMask { get; set; } = ~0;
    public Vector3 AimPointWorld { get; set; }
    public bool AimPointValid { get; set; }
    public Transform RightHandGrip { get; set; } // 可选

    public BasicBehavior(CameraMgr cameraMgr)
    {
        MainCamera = cameraMgr.getMainCamera();
        _moveBehavior = new MoveBehavior();
        _aimBehavior = new AimBehavior();
        _behaviors.Add(_moveBehavior);
        _behaviors.Add(_aimBehavior);

        foreach (var playerBehavior in _behaviors)
        {
            playerBehavior.BasicBehavior = this;
            if (playerBehavior is ITickable)
            {
                _tickables.Add(playerBehavior as ITickable);
            }
            if (playerBehavior is IAnimatorIK)
            {
                _animatorIks.Add(playerBehavior as IAnimatorIK);
            }
        }
    }

    public void RigesterAnimator(Animator animator)
    {
        Anim = animator;
        Trans = Anim.transform;
    }


    public void Tick(float dt)
    {
        for (var i = 0; i < _tickables.Count; i++)
        {
            _tickables[i].Tick(dt);
        }
    }

    public void OnAnimatorIK(int layerIndex)
    {
        for (var i = 0; i < _animatorIks.Count; i++)
        {
            _animatorIks[i].OnAnimatorIK(layerIndex);
        }
    }
}