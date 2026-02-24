using UnityEngine;

public class MoveBehavior : IPlayerBehavior, ITickable
{
    public void OnInit()
    {
        
    }

    public BasicBehavior BasicBehavior { get; set; }

    private readonly float _deadZone = 0.05f;
    private readonly float _damp = 0.2f;
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
// 瞄准：H/V 与相机相对移动方向一致（转换到角色局部）
// 角色朝向由 AimBehavior（或别的系统）控制，这里不旋转角色
// ==================================================
            Vector3 moveWorld = (camForward * input.y + camRight * input.x);
            moveWorld = Vector3.ProjectOnPlane(moveWorld, Vector3.up);

            if (moveWorld.sqrMagnitude < 0.0001f)
            {
                anim.SetFloat("H", 0f, _damp, dt);
                anim.SetFloat("V", 0f, _damp, dt);
                anim.SetFloat("Speed", 0f, _damp, dt);
                return;
            }

// 转到角色局部空间（给 Animator 用）
            Vector3 moveLocal = tr.InverseTransformDirection(moveWorld);
            moveLocal.y = 0f;

// 不要 normalized * mag，直接取分量更稳定
            float H = Mathf.Clamp(moveLocal.x, -1f, 1f);
            float V = Mathf.Clamp(moveLocal.z, -1f, 1f);

// 小死区（避免过零抖动）
            if (Mathf.Abs(H) < 0.08f) H = 0f;
            if (Mathf.Abs(V) < 0.08f) V = 0f;

// Speed 建议仍用输入强度（键盘通常是0/1）
            float Speed = mag;

// 关键：不要乘2
            anim.SetFloat("H", H, _damp, dt);
            anim.SetFloat("V", V, _damp, dt);
            anim.SetFloat("Speed", Speed, _damp, dt);
        }
    }
}