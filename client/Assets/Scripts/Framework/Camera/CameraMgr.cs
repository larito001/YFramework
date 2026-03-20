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

    private readonly Dictionary<string, CinemachineVirtualCamera> virtualCameras =
        new Dictionary<string, CinemachineVirtualCamera>(2);

    private readonly Dictionary<string, CinemachineFreeLook> freeLookCameras =
        new Dictionary<string, CinemachineFreeLook>(2);

    private SceneReferenceService sceneReferenceService;

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
            RouteSceneClick(Input.mousePosition);
        }
    }

    private void RouteSceneClick(Vector3 pointerPosition)
    {
        if (MainCamera == null || IsPointerOverUi())
        {
            return;
        }

        Ray ray = MainCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, SceneClickDistance, BuildSceneClickLayerMask()))
        {
            return;
        }

        var sceneModel = hit.collider.GetComponentInParent<SceneModelBase>();
        sceneModel?.OnMouseClick();
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
}
