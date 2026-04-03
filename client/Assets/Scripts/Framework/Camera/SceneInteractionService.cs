using UnityEngine;
using UnityEngine.EventSystems;
using YOTO;

/// <summary>
/// 场景交互服务：处理场景中物体的点击、拖拽、悬停检测。
/// 每帧只做一次 Raycast，缓存 LayerMask，拖拽使用平面投射以避免鼠标移出碰撞体时中断。
/// </summary>
public class SceneInteractionService : IGameService, ITickable
{
    private const float SceneClickDistance = 1000f;
    private const float DragStartThresholdSqr = 1f;

    private CameraMgr cameraMgr;

    // 缓存 LayerMask，避免每帧字符串查找
    private int cachedLayerMask;
    private bool layerMaskCached;

    // 每帧 Raycast 缓存
    private SceneModelBase frameHitModel;
    private RaycastHit frameHit;
    private bool frameHasHit;
    private Vector3 lastRaycastPosition;

    // 交互状态
    private SceneModelBase hoveredSceneModel;
    private SceneModelBase activeSceneModel;
    private bool isDragging;
    private Vector3 pressScreenPosition;

    // 拖拽平面：按下时记录，拖拽过程中用平面投射
    private Plane dragPlane;

    public void Init(GameContext ctx)
    {
        cameraMgr = ctx.Get<CameraMgr>();
    }

    public void Shutdown()
    {
        ClearHoveredSceneModel();
        activeSceneModel = null;
        cameraMgr = null;
        layerMaskCached = false;
    }

    public void Tick(float dt)
    {
        Camera cam = cameraMgr?.MainCamera;
        if (cam == null) return;

        // 每帧缓存震屏更新
        cameraMgr.UpdateShake(dt);

        Vector3 mousePos = Input.mousePosition;

        // 每帧只做一次 Raycast
        UpdateFrameRaycast(cam, mousePos);

        UpdateHover();

        if (Input.GetMouseButtonDown(0))
        {
            BeginPointerInteraction(cam, mousePos);
        }

        if (Input.GetMouseButton(0))
        {
            UpdatePointerInteraction(cam, mousePos);
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndPointerInteraction();
        }
    }

    private void UpdateFrameRaycast(Camera cam, Vector3 pointerPosition)
    {
        frameHitModel = null;
        frameHasHit = false;
        lastRaycastPosition = pointerPosition;

        if (IsPointerOverUi()) return;

        Ray ray = cam.ScreenPointToRay(pointerPosition);
        if (Physics.Raycast(ray, out frameHit, SceneClickDistance, GetLayerMask()))
        {
            frameHitModel = frameHit.collider.GetComponentInParent<SceneModelBase>();
            frameHasHit = frameHitModel != null;
        }
    }

    private void BeginPointerInteraction(Camera cam, Vector3 pointerPosition)
    {
        if (!frameHasHit) return;

        activeSceneModel = frameHitModel;
        isDragging = false;
        pressScreenPosition = pointerPosition;

        if (activeSceneModel.IsDraggable())
        {
            // 记录拖拽平面：过命中点，朝向相机
            dragPlane = new Plane(-cam.transform.forward, frameHit.point);
            activeSceneModel.OnMouseDown();
        }
    }

    private void UpdatePointerInteraction(Camera cam, Vector3 pointerPosition)
    {
        if (activeSceneModel == null || !activeSceneModel.IsDraggable()) return;

        if (!isDragging && !HasExceededDragThreshold(pointerPosition)) return;

        // 用拖拽平面投射，而非重新 Raycast 对象碰撞体
        Ray ray = cam.ScreenPointToRay(pointerPosition);
        if (!dragPlane.Raycast(ray, out float enter)) return;

        isDragging = true;
        Vector3 worldPoint = ray.GetPoint(enter);
        activeSceneModel.OnMouseDraging(worldPoint);
    }

    private void EndPointerInteraction()
    {
        if (activeSceneModel == null) return;

        var releasedModel = activeSceneModel;
        bool wasDragging = isDragging;

        if (releasedModel.IsDraggable())
        {
            releasedModel.OnMouseUp();
        }

        if (!wasDragging && releasedModel.IsClickable())
        {
            releasedModel.OnMouseClick();
        }

        activeSceneModel = null;
        isDragging = false;
    }

    private void UpdateHover()
    {
        if (!frameHasHit)
        {
            ClearHoveredSceneModel();
            return;
        }

        if (frameHitModel == hoveredSceneModel) return;

        ClearHoveredSceneModel();
        hoveredSceneModel = frameHitModel;
        if (hoveredSceneModel.IsHoverable())
        {
            hoveredSceneModel.OnHover();
        }
    }

    private void ClearHoveredSceneModel()
    {
        if (hoveredSceneModel == null) return;

        if (hoveredSceneModel.IsHoverable())
        {
            hoveredSceneModel.OnHoverExit();
        }

        hoveredSceneModel = null;
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
