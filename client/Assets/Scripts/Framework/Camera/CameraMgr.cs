using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using YOTO;

/// <summary>
/// Camera service responsible for scene camera lookup, virtual camera creation,
/// and lightweight click routing for scene models.
/// </summary>
public class CameraMgr : IGameService, ITickable, IFixedTickable
{
    private Camera mainCamera;
    private SceneReferenceService sceneReferenceService;
    private Vector3 touchPosition;

    // Optional shake component hosted on the main camera.
    public CameraShakeProjectile cameraShakeProjectile;

    private readonly Dictionary<string, CinemachineVirtualCamera> cameraMap =
        new Dictionary<string, CinemachineVirtualCamera>(2);

    private readonly Dictionary<string, CinemachineFreeLook> cameraMapFreeLook =
        new Dictionary<string, CinemachineFreeLook>(2);

    public bool useVCamera = false;

    public Camera getMainCamera()
    {
        return mainCamera;
    }

    public void OnShakeCamera()
    {
        cameraShakeProjectile?.ShakeCamera();
    }

    private void OnMouseUp()
    {
    }

    private void OnMouseDown(float dt)
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 screenPos = new Vector3(touchPosition.x, touchPosition.y, 0);
        mainCamera.ScreenPointToRay(screenPos);
    }

    private void Press()
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 screenPos = new Vector3(touchPosition.x, touchPosition.y, 0);
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        // Ignore world clicks while the pointer is over UI.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        int ignoreLayerMask = ~(1 << LayerMask.NameToLayer("BulletTrigger"));
        if (Physics.Raycast(ray, out var hit, 1000f, ignoreLayerMask))
        {
            GameObject obj = hit.collider.gameObject;
            if (obj.TryGetComponent(out SceneModelBase sceneModelBase))
            {
                // Scene models own their own click behavior.
                Debug.Log("Clicked ModelTrigger: " + obj.name);
                sceneModelBase.OnMouseClick();
            }
        }
    }

    #region Camera Lookup

    public CinemachineVirtualCamera getVirtualCamera(string name)
    {
        if (!cameraMap.ContainsKey(name))
        {
            cameraMap[name] = CreateCinemachineCamera(name, Vector3.zero);
        }

        return cameraMap[name];
    }

    public CinemachineFreeLook getVirtualCameraFreeLook(string name)
    {
        if (!cameraMapFreeLook.ContainsKey(name))
        {
            cameraMapFreeLook[name] = CreateCinemachineCameraFreeLook(name, Vector3.zero);
        }

        return cameraMapFreeLook[name];
    }

    private CinemachineVirtualCamera CreateCinemachineCamera(string name, Vector3 position)
    {
        GameObject cameraObject = null;
        if (sceneReferenceService.TryGetTransform(name, out var cameraTransform))
        {
            cameraObject = cameraTransform.gameObject;
        }

        CinemachineVirtualCamera vcam;
        if (cameraObject == null)
        {
            Debug.Log("Virtual camera not found, creating one at runtime: " + name);
            cameraObject = new GameObject(name);
            vcam = cameraObject.AddComponent<CinemachineVirtualCamera>();
        }
        else
        {
            vcam = cameraObject.GetComponent<CinemachineVirtualCamera>();
        }

        cameraObject.transform.position = position;
        return vcam;
    }

    private CinemachineFreeLook CreateCinemachineCameraFreeLook(string name, Vector3 position)
    {
        GameObject cameraObject = null;
        if (sceneReferenceService.TryGetTransform(name, out var cameraTransform))
        {
            cameraObject = cameraTransform.gameObject;
        }

        CinemachineFreeLook vcam;
        if (cameraObject == null)
        {
            Debug.Log("FreeLook camera not found, creating one at runtime: " + name);
            cameraObject = new GameObject(name);
            vcam = cameraObject.AddComponent<CinemachineFreeLook>();
        }
        else
        {
            vcam = cameraObject.GetComponent<CinemachineFreeLook>();
        }

        cameraObject.transform.position = position;
        return vcam;
    }

    #endregion

    public void Init(GameContext ctx)
    {
        sceneReferenceService = ctx.Get<SceneReferenceService>();
        GameObject cameraObject = null;
        if (sceneReferenceService.TryGetTransform(SceneReferenceKeys.MainCamera, out var cameraTransform))
        {
            cameraObject = cameraTransform.gameObject;
        }

        if (cameraObject == null)
        {
            Debug.LogError($"{SceneReferenceKeys.MainCamera} was not found.");
            return;
        }

        mainCamera = cameraObject.GetComponent<Camera>();
        cameraShakeProjectile = cameraObject.GetComponent<CameraShakeProjectile>();
        HudAlwaysFaceToTransform.camera = mainCamera;

        if (useVCamera)
        {
            GameObject.DontDestroyOnLoad(cameraObject);

            var brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.FixedUpdate;
            brain.m_DefaultBlend.m_Time = 0.5f;

            getVirtualCamera(SceneReferenceKeys.MainCameraVirtual);
        }
    }

    public void Shutdown()
    {
        sceneReferenceService = null;
    }

    public void Tick(float dt)
    {
        touchPosition = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            Press();
        }
    }

    public void FixedTick(float fdt)
    {
        if (Input.GetMouseButton(0))
        {
            OnMouseDown(fdt);
        }

        if (Input.GetMouseButtonUp(0))
        {
            OnMouseUp();
        }
    }
}
