using UnityEngine;

/// <summary>
/// 上半身旋转，相机对齐，枪口对准，IK
/// </summary>
public class AimBehavior : IPlayerBehavior, ITickable, IAnimatorIK
{
    public BasicBehavior BasicBehavior { get; set; }

    private readonly float _deadZone = 0.0001f;
    private readonly float _rotateLerp = 22f;
    private readonly float _damp = 0.08f;

    public void Tick(float dt)
    {
        if (BasicBehavior == null || BasicBehavior.Anim == null) return;

        var anim = BasicBehavior.Anim;
        var tr = BasicBehavior.Trans;

        bool aim = Input.GetMouseButton(1);
        anim.SetBool("Aim", aim);

        if (!aim)
        {
            BasicBehavior.AimPointValid = false;
            return;
        }

        Camera cam = BasicBehavior.MainCamera;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        Vector3 aimPoint;

        if (Physics.Raycast(ray, out RaycastHit rh, 500f, BasicBehavior.AimMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = rh.point;
        }
        else
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0f, tr.position.y, 0f));
            if (!plane.Raycast(ray, out float enter))
            {
                BasicBehavior.AimPointValid = false;
                return;
            }
            aimPoint = ray.GetPoint(enter);
        }

        Vector3 dir = Vector3.ProjectOnPlane(aimPoint - tr.position, Vector3.up);
        if (dir.sqrMagnitude < _deadZone)
        {
            BasicBehavior.AimPointValid = false;
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, _rotateLerp * dt);

        BasicBehavior.AimPointWorld = aimPoint;
        BasicBehavior.AimPointValid = true;
    }
    
    public void OnAnimatorIK(int layerIndex)
    {
        if (BasicBehavior?.Anim == null) return;

        var anim = BasicBehavior.Anim;
        bool aim = anim.GetBool("Aim");

        if (!aim || !BasicBehavior.AimPointValid)
        {
            // 退出瞄准：把权重清零
            anim.SetLookAtWeight(0f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        // 1) 上半身 / 头部对准鼠标点
        anim.SetLookAtWeight(1f, 0.6f, 0.8f, 0f, 0.6f);
        anim.SetLookAtPosition(BasicBehavior.AimPointWorld);

        // 2) （可选）右手握把 IK：需要你在 BasicBehavior 里有 RightHandGrip
        if (BasicBehavior.RightHandGrip != null)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
            anim.SetIKPosition(AvatarIKGoal.RightHand, BasicBehavior.RightHandGrip.position);
            anim.SetIKRotation(AvatarIKGoal.RightHand, BasicBehavior.RightHandGrip.rotation);
        }
        else
        {
            // 没有握把目标就别强上手IK
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        }
    }
}