using UnityEngine;

/// <summary>
/// 任何需要 Update 的系统，显式实现这些接口，GameLoop 统一调度。
/// </summary>
public interface IGameService
{
    void Init(GameContext ctx);
    void Shutdown();
}

public interface ITickable
{
    void Tick(float dt);
}

public interface IFixedTickable
{
    void FixedTick(float fdt);
}

public interface ILateTickable
{
    void LateTick(float dt);
}

public interface IAnimatorIK
{
    void OnAnimatorIK(int layerIndex);
}

public interface I2DColliderHandler
{
    void On2DColliderEnter(Collision2D other);
    void On2DColliderExit(Collision2D other);
}

public interface I2DTriggerHandler
{
    void On2DTriggerEnter(Collider2D other);
    void On2DTriggerExit(Collider2D other);
}

//3d碰撞
public interface I3DColliderHandler
{
    void On3DColliderEnter(Collision other);
    void On3DColliderExit(Collision other);
}

public interface I3DTriggerHandler
{
    void On3DTriggerEnter(Collider other);
    void On3DTriggerExit(Collider other);
}

public interface IClickable
{
    void OnClick();
}

public interface IHoverable
{
    void OnHover();
    void OnHoverExit();
}

public interface IDraggable
{
    void OnMouseDown();
    void OnMouseDrag(Vector3 worldPosition);
    void OnMouseUp();
}
