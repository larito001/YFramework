using UnityEngine;

/// <summary>
/// 子弹 view：被动同步 Bullet.Position 到 transform，朝向沿 Velocity。
/// 没有自己的更新逻辑 —— 所有飞行/命中都在逻辑层（BulletMoveComponent）处理。
/// 运行时 AddComponent 到子弹 prefab 实例（或 BulletManager fallback 创建的 primitive Sphere）。
/// </summary>
public class BulletView : BaseView
{
    private Bullet bullet;

    public override void Bind(Actor actor, int id)
    {
        bullet = actor as Bullet;
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
    }
}
