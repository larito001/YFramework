using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using YOTO;

/// <summary>
/// 纯相机管理服务：主相机查找、Cinemachine 虚拟相机管理、相机震屏。
/// 场景交互（点击/拖拽/悬停）已拆分至 <see cref="SceneInteractionService"/>。
/// </summary>
public class CameraMgr : MonoBehaviour
{
    private readonly Dictionary<string, CinemachineVirtualCamera> virtualCameras =
        new Dictionary<string, CinemachineVirtualCamera>(2);

    private readonly Dictionary<string, CinemachineFreeLook> freeLookCameras =
        new Dictionary<string, CinemachineFreeLook>(2);

    private SceneReferenceService sceneReferenceService;

    // 震屏状态
    private float shakeDuration;
    private float shakeTimer;
    private float shakeIntensity;
    private Vector3 originalLocalPosition;
    private Transform cameraTransform;
    private bool isShaking;

    public Camera MainCamera { get; private set; }
    public bool UseVirtualCamera { get; set; }

    public void ShakeCamera(float duration = 0.3f, float intensity = 0.15f)
    {
        if (MainCamera == null) return;

        cameraTransform = MainCamera.transform;
        if (!isShaking)
        {
            originalLocalPosition = cameraTransform.localPosition;
        }

        shakeDuration = duration;
        shakeTimer = duration;
        shakeIntensity = intensity;
        isShaking = true;
    }

    public void UpdateShake(float dt)
    {
        if (!isShaking) return;

        if (shakeTimer > 0f)
        {
            float progress = shakeTimer / shakeDuration;
            float currentIntensity = shakeIntensity * progress; // 衰减
            Vector3 offset = new Vector3(
                Random.Range(-currentIntensity, currentIntensity),
                Random.Range(-currentIntensity, currentIntensity),
                0f
            );
            cameraTransform.localPosition = originalLocalPosition + offset;
            shakeTimer -= dt;
        }
        else
        {
            cameraTransform.localPosition = originalLocalPosition;
            isShaking = false;
        }
    }

    public CinemachineVirtualCamera GetVirtualCamera(string name)
    {
        return GetOrCreateCachedCamera(name, virtualCameras);
    }

    public CinemachineFreeLook GetVirtualCameraFreeLook(string name)
    {
        return GetOrCreateCachedCamera(name, freeLookCameras);
    }

    private void Awake()
    {
        sceneReferenceService = GameLoop.Instance.Ctx.Get<SceneReferenceService>();
        if (!TryResolveMainCamera(out var mainCamera))
        {
            Debug.LogError($"{SceneRefKeys.MainCamera} was not found or does not have a Camera component.");
            return;
        }

        MainCamera = mainCamera;

        if (UseVirtualCamera)
        {
            EnableVirtualCameraPipeline();
        }
    }

    private void OnDestroy()
    {
        if (isShaking && cameraTransform != null)
        {
            cameraTransform.localPosition = originalLocalPosition;
        }

        isShaking = false;
        MainCamera = null;
        sceneReferenceService = null;
        virtualCameras.Clear();
        freeLookCameras.Clear();
    }

    private bool TryResolveMainCamera(out Camera camera)
    {
        camera = null;
        if (sceneReferenceService == null ||
            !sceneReferenceService.TryGetTransform(SceneRefKeys.MainCamera, out var camTransform))
        {
            return false;
        }

        camera = camTransform.GetComponent<Camera>();
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

        GetVirtualCamera(SceneRefKeys.MainCameraVirtual);
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
}
