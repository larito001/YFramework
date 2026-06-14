using UnityEngine;

/// <summary>
/// 子弹 view：被动同步 Bullet.Position 到 transform，朝向沿 Velocity。
/// 没有自己的更新逻辑 —— 所有飞行/命中都在逻辑层（BulletMoveComponent）处理。
///
/// 走 PrefabPool 池化：OnDespawn 清字段避免下次取出时还引用上一个 bullet。
/// LateUpdate 检查 bullet=null 早退，防止 pool Release 后到下次 Get 之间的边界帧。
/// </summary>
public class BulletView : BaseView, IPoolable
{
    private Bullet bullet;

    public override void Bind(Actor actor, int id)
    {
        bullet = actor as Bullet;
        Owner = actor; // 池化 view 由 BulletManager 直接 Bind（不走 ViewManager），这里自行注入 Owner 供迷雾剔除
        ID = id;
        if (bullet != null)
        {
            transform.position = bullet.Position;
            if (bullet.Velocity.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(bullet.Velocity);
        }
    }

    private void LateUpdate()
    {
        if (bullet == null) return;
        transform.position = bullet.Position;
        if (bullet.Velocity.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(bullet.Velocity);
        ApplyFogVisibility(); // 战争迷雾遮挡剔除：读 Owner.Visible 开关自身 renderer
    }

    public void OnSpawn() { /* Bind 紧跟在 Get 之后调用，这里无需操作 */ }

    public void OnDespawn()
    {
        ResetFogVisibility(); // 池化复用前复位迷雾隐藏态，避免下次取出残留隐藏
        bullet = null;
        Owner = null;
        ID = -1;
    }
}
