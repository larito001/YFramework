using UnityEngine;
using UnityEngine.EventSystems;
using YOTO;

/// <summary>
/// 场景交互服务：处理场景中物体的点击、拖拽、悬停检测。
/// 每帧只做一次 Raycast，缓存 LayerMask，拖拽使用平面投射以避免鼠标移出碰撞体时中断。
/// 命中物上的 IClickable / IHoverable / IDraggable 接口由 GetComponentInParent 直接查找，
/// 不再走 SceneModelBase 转发。
/// </summary>
public class SceneInteractionService : MonoBehaviour
{
    private const float SceneClickDistance = 1000f;
    private const float DragStartThresholdSqr = 1f;

    private CameraMgr cameraMgr;

    private int cachedLayerMask;
    private bool layerMaskCached;

    private bool frameHasHit;
    private RaycastHit frameHit;
    private IClickable frameClickable;
    private IHoverable frameHoverable;
    private IDraggable frameDraggable;

    private IHoverable hoveredHoverable;
    private IClickable activeClickable;
    private IDraggable activeDraggable;
    private bool isDragging;
    private Vector3 pressScreenPosition;
    private Plane dragPlane;

    private void Awake()
    {
        cameraMgr = GameLoop.Instance.Ctx.Get<CameraMgr>();
    }

    private void OnDestroy()
    {
        ClearHover();
        activeClickable = null;
        activeDraggable = null;
        cameraMgr = null;
        layerMaskCached = false;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Camera cam = cameraMgr?.MainCamera;
        if (cam == null) return;

        cameraMgr.UpdateShake(dt);
        Vector3 mousePos = Input.mousePosition;

        UpdateFrameRaycast(cam, mousePos);
        UpdateHover();

        if (Input.GetMouseButtonDown(0)) BeginPointerInteraction(cam, mousePos);
        if (Input.GetMouseButton(0))      UpdatePointerInteraction(cam, mousePos);
        if (Input.GetMouseButtonUp(0))    EndPointerInteraction();
    }

    private void UpdateFrameRaycast(Camera cam, Vector3 pointerPosition)
    {
        frameHasHit = false;
        frameClickable = null;
        frameHoverable = null;
        frameDraggable = null;

        if (IsPointerOverUi()) return;

        Ray ray = cam.ScreenPointToRay(pointerPosition);
        if (Physics.Raycast(ray, out frameHit, SceneClickDistance, GetLayerMask()))
        {
            var collider = frameHit.collider;
            frameClickable = collider.GetComponentInParent<IClickable>();
            frameHoverable = collider.GetComponentInParent<IHoverable>();
            frameDraggable = collider.GetComponentInParent<IDraggable>();
            frameHasHit = frameClickable != null || frameHoverable != null || frameDraggable != null;
        }
    }

    private void BeginPointerInteraction(Camera cam, Vector3 pointerPosition)
    {
        if (!frameHasHit) return;

        activeClickable = frameClickable;
        activeDraggable = frameDraggable;
        isDragging = false;
        pressScreenPosition = pointerPosition;

        if (activeDraggable != null)
        {
            dragPlane = new Plane(-cam.transform.forward, frameHit.point);
            activeDraggable.OnMouseDown();
        }
    }

    private void UpdatePointerInteraction(Camera cam, Vector3 pointerPosition)
    {
        if (activeDraggable == null) return;

        if (!isDragging && !HasExceededDragThreshold(pointerPosition)) return;

        Ray ray = cam.ScreenPointToRay(pointerPosition);
        if (!dragPlane.Raycast(ray, out float enter)) return;

        isDragging = true;
        Vector3 worldPoint = ray.GetPoint(enter);
        activeDraggable.OnMouseDrag(worldPoint);
    }

    private void EndPointerInteraction()
    {
        if (activeClickable == null && activeDraggable == null) return;

        bool wasDragging = isDragging;

        if (activeDraggable != null)
        {
            activeDraggable.OnMouseUp();
        }

        if (!wasDragging && activeClickable != null)
        {
            activeClickable.OnClick();
        }

        activeClickable = null;
        activeDraggable = null;
        isDragging = false;
    }

    private void UpdateHover()
    {
        if (!frameHasHit || frameHoverable == null)
        {
            ClearHover();
            return;
        }

        if (frameHoverable == hoveredHoverable) return;

        ClearHover();
        hoveredHoverable = frameHoverable;
        hoveredHoverable.OnHover();
    }

    private void ClearHover()
    {
        if (hoveredHoverable == null) return;
        hoveredHoverable.OnHoverExit();
        hoveredHoverable = null;
    }

    private int GetLayerMask()
    {
        if (layerMaskCached) return cachedLayerMask;

        int bulletTriggerLayer = LayerMask.NameToLayer("BulletTrigger");
        cachedLayerMask = bulletTriggerLayer < 0
            ? Physics.DefaultRaycastLayers
            : ~(1 << bulletTriggerLayer);
        layerMaskCached = true;
        return cachedLayerMask;
    }

    private bool HasExceededDragThreshold(Vector3 pointerPosition)
    {
        return (pointerPosition - pressScreenPosition).sqrMagnitude >= DragStartThresholdSqr;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
