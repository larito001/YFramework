using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using YOTO;


public class CameraMgr
{
    private Camera mainCamera;
    private Vector3 touchPosition;
    private bool isInit = false;

    private Dictionary<string, CinemachineVirtualCamera>
        cameraMap = new Dictionary<string, CinemachineVirtualCamera>(2);

    private Dictionary<string, CinemachineFreeLook> cameraMapFreeLook = new Dictionary<string, CinemachineFreeLook>(2);

    public Camera getMainCamera()
    {
        return mainCamera;
    }

    public bool useVCamera = false;
    public void Init(bool useVCamera =false)
    {
        GameObject cameraObject = GameObject.Find("MainCamera");
        this.useVCamera=useVCamera;
        mainCamera = cameraObject.GetComponent<Camera>();
        if (useVCamera)
        {
      
            GameObject.DontDestroyOnLoad(cameraObject);
     
            var brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.FixedUpdate;
            brain.m_DefaultBlend.m_Time = 0.5f;

            getVirtualCamera("MainCameraVirtual");
        }
    

        isInit = true;
    }

    public void Update(float dt)
    {
        touchPosition = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            Press();
        }
    }
    
    private void Press()
    {
        List<RaycastResult> results = new List<RaycastResult>();
        Vector3 dir = new Vector3(touchPosition.x, touchPosition.y, touchPosition.z);
        Ray ray = YOTOFramework.cameraMgr.getMainCamera().ScreenPointToRay(dir);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo, 1000))
        {
            // todo: 点击逻辑
        }
    }

    #region 获取相机

    public CinemachineVirtualCamera getVirtualCamera(string name)
    {
        if (!cameraMap.ContainsKey(name))
        {
            cameraMap[name] = CreateCinemachineCamera(name, position: new Vector3(0, 0, 0));
        }

        return cameraMap[name];
    }

    public CinemachineFreeLook getVirtualCameraFreeLook(string name)
    {
        if (!cameraMapFreeLook.ContainsKey(name))
        {
            cameraMapFreeLook[name] = CreateCinemachineCameraFreeLook(name, position: new Vector3(0, 0, 0));
        }

        return cameraMapFreeLook[name];
    }

    public static CinemachineVirtualCamera CreateCinemachineCamera(string name, Vector3 position)
    {
        GameObject cameraObject;
        cameraObject = GameObject.Find(name);
        CinemachineVirtualCamera vcam;
        if (!cameraObject)
        {
            Debug.Log("未找到虚拟相机" + name);
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

    public static CinemachineFreeLook CreateCinemachineCameraFreeLook(string name, Vector3 position)
    {
        GameObject cameraObject;
        cameraObject = GameObject.Find(name);
        CinemachineFreeLook vcam;
        if (!cameraObject)
        {
            Debug.Log("δ�ҵ����õ��������" + name);
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
}