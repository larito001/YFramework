using UnityEngine;

/// <summary>
/// 俯视角瞄准（纯逻辑组件，不碰 view）：
///   - 每帧把鼠标光标投射到与角色等高的水平面，得到世界瞄准点 → Owner.AimTargetWorldPos
///   - 把角色 Owner.Rotation 平滑转向那个点
/// 添加顺序：必须在 MoveComponent 之前 Add，让 MoveComponent 用最新 Rotation 算 local 动画参数。
/// 不需要瞄准的角色（NPC / AI）就别 Add 这个组件，自己写 AI 朝向覆盖 Owner.Rotation。
/// </summary>
public class AimComponent : ICharacterComponent
{
    public float RotateSpeed = 1080f; // deg/s，俯视角射击要跟手

    private InputService input;
    private CameraManager cameraMgr;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx == null)
        {
            Debug.LogError("[AimComponent] GameLoop.Ctx 未就绪");
            return;
        }
        ctx.TryGet(out input);
        ctx.TryGet(out cameraMgr);
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;

        var cam = cameraMgr != null ? cameraMgr.MainCamera : Camera.main;
        if (cam == null) return;

        var ray = cam.ScreenPointToRay(input.MousePosition);
        // 与角色等高的水平面，避免角色站在台阶上时投射点跑偏
        var plane = new Plane(Vector3.up, new Vector3(0f, Owner.Position.y, 0f));
        if (!plane.Raycast(ray, out float enter)) return;

        var hit = ray.GetPoint(enter);
        Owner.AimTargetWorldPos = hit;

        var dir = hit - Owner.Position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;

        var targetRot = Quaternion.LookRotation(dir, Vector3.up);
        Owner.Rotation = Quaternion.RotateTowards(Owner.Rotation, targetRot, RotateSpeed * dt);
    }
}
