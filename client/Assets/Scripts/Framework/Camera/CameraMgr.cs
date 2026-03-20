using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using YOTO;

/// <summary>
/// Centralized camera service for main camera lookup, scene click routing, and
/// runtime Cinemachine camera access.
/// </summary>
public class CameraMgr : IGameService, ITickable
{
    private const float SceneClickDistance = 1000f;
    private const float DragStartThresholdPixels = 10f;

    private readonly Dictionary<string, CinemachineVirtualCamera> virtualCameras =
        new Dictionary<string, CinemachineVirtualCamera>(2);

    private readonly Dictionary<string, CinemachineFreeLook> freeLookCameras =
        new Dictionary<string, CinemachineFreeLook>(2);

    private SceneReferenceService sceneReferenceService;
    private SceneModelBase activeSceneModel;
    private Vector3 pressScreenPosition;
    private Plane dragPlane;
    private bool isDragging;

    public Camera MainCamera { get; private set; }
    public CameraShakeProjectile CameraShakeProjectile { get; private set; }
    public bool UseVirtualCamera { get; set; }

    public void ShakeCamera()
    {
        CameraShakeProjectile?.ShakeCamera();
    }

    public CinemachineVirtualCamera GetVirtualCamera(string name)
    {
        return GetOrCreateCachedCamera(name, virtualCameras);
    }

    public CinemachineFreeLook GetVirtualCameraFreeLook(string name)
    {
        return GetOrCreateCachedCamera(name, freeLookCameras);
    }

    public void Init(GameContext ctx)
    {
        sceneReferenceService = ctx.Get<SceneReferenceService>();
        if (!TryResolveMainCamera(out var mainCamera))
        {
            Debug.LogError($"{SceneReferenceKeys.MainCamera} was not found or does not have a Camera component.");
            return;
        }

        MainCamera = mainCamera;
        CameraShakeProjectile = MainCamera.GetComponent<CameraShakeProjectile>();

        if (UseVirtualCamera)
        {
            EnableVirtualCameraPipeline();
        }
    }

    public void Shutdown()
    {
        MainCamera = null;
        CameraShakeProjectile = null;
        sceneReferenceService = null;
        virtualCameras.Clear();
        freeLookCameras.Clear();
    }

    public void Tick(float dt)
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginPointerInteraction(Input.mousePosition);
        }

        if (Input.GetMouseButton(0))
        {
            UpdatePointerInteraction(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndPointerInteraction(Input.mousePosition);
        }
    }

    private void BeginPointerInteraction(Vector3 pointerPosition)
    {
        if (MainCamera == null || IsPointerOverUi())
        {
            return;
        }

        if (!TryRaycastSceneModel(pointerPosition, out var sceneModel, out var hit))
        {
            return;
        }

        activeSceneModel = sceneModel;
        pressScreenPosition = pointerPosition;
        dragPlane = new Plane(Vector3.up, hit.point);
        isDragging = false;
        activeSceneModel.OnMouseDown();
    }

    private void UpdatePointerInteraction(Vector3 pointerPosition)
    {
        if (MainCamera == null || activeSceneModel == null)
        {
            return;
        }

        if (!isDragging && !HasExceededDragThreshold(pointerPosition))
        {
            return;
        }

        if (!TryGetDragWorldPoint(pointerPosition, out var worldPoint))
        {
            return;
        }

        isDragging = true;
        activeSceneModel.OnMouseDrag(worldPoint);
    }

    private void EndPointerInteraction(Vector3 pointerPosition)
    {
        if (activeSceneModel == null)
        {
            return;
        }

        var releasedSceneModel = activeSceneModel;
        bool wasDragging = isDragging;

        if (wasDragging && TryGetDragWorldPoint(pointerPosition, out var worldPoint))
        {
            releasedSceneModel.OnMouseDrag(worldPoint);
        }

        releasedSceneModel.OnMouseUp();
        if (!wasDragging)
        {
            releasedSceneModel.OnMouseClick();
        }

        activeSceneModel = null;
        isDragging = false;
    }

    private bool TryRaycastSceneModel(Vector3 pointerPosition, out SceneModelBase sceneModel, out RaycastHit hit)
    {
        sceneModel = null;
        Ray ray = MainCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out hit, SceneClickDistance, BuildSceneClickLayerMask()))
        {
            return false;
        }

        sceneModel = hit.collider.GetComponentInParent<SceneModelBase>();
        return sceneModel != null;
    }

    private bool TryGetDragWorldPoint(Vector3 screenPosition, out Vector3 worldPoint)
    {
        worldPoint = default;
        Ray ray = MainCamera.ScreenPointToRay(screenPosition);
        if (!dragPlane.Raycast(ray, out var enter))
        {
            return false;
        }

        worldPoint = ray.GetPoint(enter);
        return true;
    }

    private bool TryResolveMainCamera(out Camera camera)
    {
        camera = null;
        if (sceneReferenceService == null ||
            !sceneReferenceService.TryGetTransform(SceneReferenceKeys.MainCamera, out var cameraTransform))
        {
            return false;
        }

        camera = cameraTransform.GetComponent<Camera>();
        return camera != null;
    }

    private void EnableVirtualCameraPipeline()
    {
        GameObject.DontDestroyOnLoad(MainCamera.gameObject);

        var brain = MainCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = MainCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.FixedUpdate;
        brain.m_DefaultBlend.m_Time = 0.5f;

        GetVirtualCamera(SceneReferenceKeys.MainCameraVirtual);
    }

    private TCamera GetOrCreateCachedCamera<TCamera>(string name, Dictionary<string, TCamera> cache)
        where TCamera : Component
    {
        if (cache.TryGetValue(name, out var camera) && camera != null)
        {
            return camera;
        }

        camera = GetOrCreateSceneComponent<TCamera>(name);
        cache[name] = camera;
        return camera;
    }

    private TComponent GetOrCreateSceneComponent<TComponent>(string name) where TComponent : Component
    {
        GameObject targetObject = null;
        if (sceneReferenceService != null && sceneReferenceService.TryGetTransform(name, out var targetTransform))
        {
            targetObject = targetTransform.gameObject;
        }

        if (targetObject == null)
        {
            Debug.LogWarning($"Scene camera '{name}' was not found. Creating it at runtime.");
            targetObject = new GameObject(name);
        }

        var component = targetObject.GetComponent<TComponent>();
        if (component == null)
        {
            component = targetObject.AddComponent<TComponent>();
        }

        return component;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static int BuildSceneClickLayerMask()
    {
        int bulletTriggerLayer = LayerMask.NameToLayer("BulletTrigger");
        if (bulletTriggerLayer < 0)
        {
            return Physics.DefaultRaycastLayers;
        }

        return ~(1 << bulletTriggerLayer);
    }

    private bool HasExceededDragThreshold(Vector3 pointerPosition)
    {
        return (pointerPosition - pressScreenPosition).sqrMagnitude >=
               DragStartThresholdPixels * DragStartThresholdPixels;
    }
}
