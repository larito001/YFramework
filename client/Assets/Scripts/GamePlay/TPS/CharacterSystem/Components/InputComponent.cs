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
            input.OnFireDown += HandleFireDown;      // 左键按下 → 近战武器放第一个技能（常规枪走 FireHeld 开火）
            input.OnMeleeDown += HandleMelee;        // V 键 → 释放第二个技能（常规枪回退技能 0 近战）
            input.OnDodgeDown += RaiseDodge;         // 空格 → 闪避（DodgeComponent 订阅 OnDodge）
            input.OnReloadDown += RaiseReload;
            input.OnWeaponSelect += RaiseWeaponSelect;
        }
    }

    public override void Detach()
    {
        if (input != null)
        {
            input.OnFireDown -= HandleFireDown;
            input.OnMeleeDown -= HandleMelee;
            input.OnDodgeDown -= RaiseDodge;
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
        // 瞄准常驻：默认始终瞄准（面向鼠标 + AimSpeed 移动）；按住 Shift 进入冲刺（暂时退出瞄准、朝移动方向跑、SprintSpeed），
        // 松开即回到瞄准。瞄准与冲刺互斥，所以这里直接 AimHeld = !SprintHeld（不再读 RMB）。
        AimHeld = !input.SprintHeld;
        // 近战/技能武器（WeaponPrimarySkill>=0）：左键改放技能（见 HandleFireDown），不写开火意图。常规枪正常持续开火。
        FireHeld = Owner.WeaponPrimarySkill >= 0 ? false : input.FireHeld;

        // 瞄准世界点：仅瞄准时算（非瞄准不做无谓射线，且 AimWorldPoint 契约就是"AimHeld 时有效"）。
        // 鼠标射线 ∩ 枪口高度平面（俯视倾斜相机下"点哪打哪"靠这层平面）。
        if (AimHeld)
        {
            bool got = false;
            var cam = cameraMgr != null ? cameraMgr.MainCamera : Camera.main;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(input.MousePosition);
                float planeY = Owner.Position.y + Owner.MuzzleHeight;
                var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
                if (plane.Raycast(ray, out float enter)) { AimWorldPoint = ray.GetPoint(enter); got = true; }
            }
            // 兜底：相机未就绪 / 射线未命中时朝角色正前方，绝不退化成世界原点（否则首帧瞄准会瞬转朝 (0,0,0)）
            if (!got)
            {
                var fwd = Owner.Rotation * Vector3.forward;
                fwd.y = 0f;
                AimWorldPoint = Owner.Position + (fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward);
            }
        }
    }

    /// <summary>左键按下 → 语义"轻击"（Light）。<see cref="ComboComponent"/> 接管：有连招图走图、无图回退单招（WeaponPrimarySkill）、
    /// 常规枪（WeaponPrimarySkill&lt;0）则被 ComboComponent 忽略、左键开火照常走 FireHeld。这里不再直接判技能下标。</summary>
    private void HandleFireDown() => RaiseAttack(ComboButton.Light);

    /// <summary>V 键 → 语义"重击"（Heavy）。ComboComponent 接管：有图走图、无图回退单招（WeaponSecondarySkill，-1 回退技能 0 = 旧"V 近战"）。</summary>
    private void HandleMelee() => RaiseAttack(ComboButton.Heavy);
}
