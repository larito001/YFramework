using UnityEngine;

/// <summary>
/// **玩家**输入组件：<see cref="InputService"/>（+ 相机）的唯一消费者。把键鼠原始输入解释成
/// <see cref="InputComponentBase"/> 的世界空间意图——WASD→相机基底世界方向、鼠标→枪口高度平面世界点、
/// V/R/数字键→技能/换弹/选武器事件。玩法组件只读意图面，不再碰 InputService / 相机。
/// </summary>
public class InputComponent : InputComponentBase
{
    private InputService input;
    private CameraManager cameraMgr;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out input);
        Ctx?.TryGet(out cameraMgr);
        if (input != null)
        {
            input.OnMeleeDown += HandleMelee;        // V 键 → 释放近战技能（下标 0）
            input.OnReloadDown += RaiseReload;
            input.OnWeaponSelect += RaiseWeaponSelect;
        }
    }

    public override void Detach()
    {
        if (input != null)
        {
            input.OnMeleeDown -= HandleMelee;
            input.OnReloadDown -= RaiseReload;
            input.OnWeaponSelect -= RaiseWeaponSelect;
        }
        input = null;
        cameraMgr = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;

        // 移动意图：WASD（相机基底）→ 世界方向
        var m = input.Move;
        Vector3 dir = Vector3.zero;
        if (m.sqrMagnitude > 1e-4f)
        {
            var camFwd = cameraMgr != null ? cameraMgr.PlanarForward : Vector3.forward;
            var camRight = cameraMgr != null ? cameraMgr.PlanarRight : Vector3.right;
            dir = camFwd * m.y + camRight * m.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
        }
        MoveWorld = dir;

        SprintHeld = input.SprintHeld;
        AimHeld = input.AimHeld;
        FireHeld = input.FireHeld;

        // 瞄准世界点：鼠标射线 ∩ 枪口高度平面（俯视倾斜相机下"点哪打哪"靠这层平面）
        var cam = cameraMgr != null ? cameraMgr.MainCamera : Camera.main;
        if (cam != null)
        {
            var ray = cam.ScreenPointToRay(input.MousePosition);
            float planeY = Owner.Position.y + Owner.MuzzleHeight;
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (plane.Raycast(ray, out float enter))
                AimWorldPoint = ray.GetPoint(enter);
        }
    }

    private void HandleMelee() => RaiseCastSkill(0);
}
