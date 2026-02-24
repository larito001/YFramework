using UnityEngine;

/// <summary>
/// 上半身旋转，相机对齐，枪口对准，IK
/// </summary>
public class AimBehavior : IPlayerBehavior, ITickable, IAnimatorIK
{
    private Vector3 initialRootRotation; // Initial root bone local rotation.
    private Vector3 initialHipsRotation; // Initial hips rotation related to the root bone.
    private Vector3 initialSpineRotation; // Initial spine rotation related to the root bone.

    public void OnInit()
    {
        Transform hips = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Hips);
        Transform spine = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Spine);
        Transform root = hips.parent;
        if (spine.parent != hips)
        {
            root = hips;
            hips = spine.parent;
        }

        initialRootRotation = (root == BasicBehavior.Trans) ? Vector3.zero : root.localEulerAngles;
        initialHipsRotation = hips.localEulerAngles;
        initialSpineRotation = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Spine).localEulerAngles;
    }

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
            float playerHeight = 1.2f;

            Vector3 n = rh.normal.normalized;         // 地面法线
            Vector3 ro = ray.origin;                  // 射线起点
            Vector3 rd = ray.direction.normalized;    // 射线方向
            Vector3 p0 = rh.point + n * playerHeight; // 目标平面上的一点（抬高后的平面）

            // 手动计算射线与平面交点
            float denom = Vector3.Dot(n, rd);

            if (Mathf.Abs(denom) > 1e-6f) // 不平行
            {
                float t = Vector3.Dot(n, p0 - ro) / denom;

                if (t >= 0f)
                {
                    aimPoint = ro + rd * t;
                }
                else
                {
                    // 交点在射线反方向（通常不该发生），兜底
                    aimPoint = p0;
                }
            }
            else
            {
                // 射线与平面平行，兜底
                aimPoint = p0;
            }
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

        // if (!aim || !BasicBehavior.AimPointValid)
        // {
        //     // 退出瞄准：把权重清零
        //     anim.SetLookAtWeight(0f);
        //     anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        //     anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        //     return;
        // }
        //
        // // 1) 上半身 / 头部对准鼠标点
        // anim.SetLookAtWeight(1f, 0.6f, 0.8f, 0f, 0.6f);
        // anim.SetLookAtPosition(BasicBehavior.AimPointWorld);
        //
        // // 2) （可选）右手握把 IK：需要你在 BasicBehavior 里有 RightHandGrip
        // if (BasicBehavior.RightHandGrip != null)
        // {
        //     anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        //     anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
        //     anim.SetIKPosition(AvatarIKGoal.RightHand, BasicBehavior.RightHandGrip.position);
        //     anim.SetIKRotation(AvatarIKGoal.RightHand, BasicBehavior.RightHandGrip.rotation);
        // }
        // else
        // {
        //     // 没有握把目标就别强上手IK
        //     anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        //     anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        // }
    }
}