using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器管理、开火、射线检测、后坐力、武器切换
/// </summary>
public class ShootBehaviour : IPlayerBehavior, ITickable, IAnimatorIK
{
    private Transform hips, spine, chest, rightHand, leftArm;
    private Vector3 initialRootRotation; // Initial root bone local rotation.
    private Vector3 initialHipsRotation; // Initial hips rotation related to the root bone.
    private Vector3 initialSpineRotation; // Initial spine rotation related to the hips bone.
    private Vector3 initialChestRotation;

    public void OnInit()
    {
        Transform neck = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Neck);
        if (!neck)
        {
            neck = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Head).parent;
        }

        hips = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Hips);
        spine = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Spine);
        chest = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.Chest);
        rightHand = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.RightHand);
        leftArm = BasicBehavior.Anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform root = hips.parent;
        if (spine.parent != hips)
        {
            root = hips;
            hips = spine.parent;
        }

        // Get initial values.
        initialRootRotation = (root == BasicBehavior.Trans) ? Vector3.zero : root.localEulerAngles;
        initialHipsRotation = hips.localEulerAngles;
        initialSpineRotation = spine.localEulerAngles;
        initialChestRotation = chest.localEulerAngles;
    }

    public BasicBehavior BasicBehavior { get; set; }

    public void Tick(float dt)
    {
    }

    public void OnAnimatorIK(int layerIndex)
    {
        if (BasicBehavior == null || BasicBehavior.Anim == null) return;
        // Orientate upper body where camera  is targeting.
        Quaternion targetRot = Quaternion.Euler(0, BasicBehavior.Trans.eulerAngles.y, 0);
        targetRot *= Quaternion.Euler(initialRootRotation);
        targetRot *= Quaternion.Euler(initialHipsRotation);
        targetRot *= Quaternion.Euler(initialSpineRotation);
        // Set upper body horizontal orientation.
        BasicBehavior.Anim.SetBoneLocalRotation(HumanBodyBones.Spine,
            Quaternion.Inverse(hips.rotation) * targetRot);

        // Keep upper body orientation regardless strafe direction.
        float xCamRot = Quaternion.LookRotation(BasicBehavior.Trans.forward+new Vector3(0,-0.1f,0)).eulerAngles.x;
        targetRot = Quaternion.AngleAxis(xCamRot, BasicBehavior.Trans.right);
        // Correction for long weapons.
        targetRot *= Quaternion.AngleAxis(9f, BasicBehavior.Trans.right);
        targetRot *= Quaternion.AngleAxis(20f, BasicBehavior.Trans.up);
        targetRot *= spine.rotation;
        targetRot *= Quaternion.Euler(initialChestRotation);
        // Set upper body vertical orientation.
        BasicBehavior.Anim.SetBoneLocalRotation(HumanBodyBones.Chest,
            Quaternion.Inverse(spine.rotation) * targetRot);
    }
}