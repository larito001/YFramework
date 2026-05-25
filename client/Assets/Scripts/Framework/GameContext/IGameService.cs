using UnityEngine;

// 生命周期接口（IGameService / ITickable / IFixedTickable / ILateTickable）已退役
// —— 各 Mgr 直接继承 MonoBehaviour，由 Unity 原生 Awake / Update / FixedUpdate /
// LateUpdate / OnDestroy 驱动。本文件保留交互/业务标记接口供场景实体实现。

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
