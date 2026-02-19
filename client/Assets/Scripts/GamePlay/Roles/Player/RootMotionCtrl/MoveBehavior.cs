using UnityEngine;

public class MoveBehavior : IPlayerBehavior, ITickable
{
    public BasicBehavior BasicBehavior { get; set; }

    private readonly float _deadZone = 0.05f;
    private readonly float _damp = 1f;
    private readonly float _rotateLerp = 18f;

    public void Tick(float dt)
    {
        if (BasicBehavior == null || BasicBehavior.Anim == null) return;

        var anim = BasicBehavior.Anim;
        var tr = BasicBehavior.Trans;

        bool aim = anim.GetBool("Aim");

        // 1) 读取 WASD
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector2 input = new Vector2(h, v);
        float mag = Mathf.Clamp01(input.magnitude);

        // ===== 相机基向量（两种状态都可能用到）=====
        Camera cam = BasicBehavior.MainCamera;
        Vector3 camForward, camRight;

        if (cam != null)
        {
            camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
        }
        else
        {
            camForward = tr.forward;
            camRight   = tr.right;
        }

        // 没输入：都停
        if (mag < _deadZone)
        {
            anim.SetFloat("H", 0f, _damp, dt);
            anim.SetFloat("V", 0f, _damp, dt);
            anim.SetFloat("Speed", 0f, _damp, dt);
            return;
        }

        if (!aim)
        {
            // ==================================================
            // 非瞄准：保持你原来的逻辑（相机相对 + 只前进）
            // ==================================================

            // 相机相对的期望方向
            Vector3 desiredDir = (camForward * input.y + camRight * input.x);
            desiredDir = Vector3.ProjectOnPlane(desiredDir, Vector3.up);

            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                desiredDir.Normalize();

                // 非瞄准：角色转向该方向，然后动画只做“向前走”
                Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
                tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, _rotateLerp * dt);
            }

            float Speed = mag; // 强度(0~1)
            anim.SetFloat("H", 0f, _damp, dt);
            anim.SetFloat("V", 0f, _damp, dt);
            anim.SetFloat("Speed", Speed, _damp, dt);
        }
        else
        {
            // ==================================================
            // 瞄准：H/V 与“相机相对移动方向”一致（不跟角色朝向绑死）
            // 角色朝向由 AimBehavior 对准鼠标，这里不再旋转角色
            // ==================================================

            // 1) 相机相对世界移动方向（W=camForward, A= -camRight）
            Vector3 moveWorld = (camForward * input.y + camRight * input.x);
            moveWorld = Vector3.ProjectOnPlane(moveWorld, Vector3.up);

            // 2) 转到角色局部空间，得到 Animator 需要的 H/V
            // local.x = 左右, local.z = 前后
            Vector3 moveLocal = tr.InverseTransformDirection(moveWorld);
            moveLocal.y = 0f;

            // 归一化后按输入强度缩放，保证 H/V 在 [-1,1] 附近
            Vector2 hv = new Vector2(moveLocal.x, moveLocal.z);
            if (hv.sqrMagnitude > 0.0001f) hv = hv.normalized * mag;
            else hv = Vector2.zero;

            float H = Mathf.Clamp(hv.x, -1f, 1f);
            float V = Mathf.Clamp(hv.y, -1f, 1f);
            float Speed = mag;

            anim.SetFloat("H", H, _damp, dt);
            anim.SetFloat("V", V, _damp, dt);
            anim.SetFloat("Speed", Speed, _damp, dt);
        }
    }
}