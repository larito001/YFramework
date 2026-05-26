using Cinemachine;
using UnityEngine;

/// <summary>
/// 相机绑架（Rig）：解析主相机、按需挂载 CinemachineBrain。
/// 仅承载相机引用与渲染管线开关，不包含交互、震屏、虚拟相机切换逻辑。
/// </summary>
public class CameraRig
{
    public Camera MainCamera { get; private set; }
    public CinemachineBrain Brain { get; private set; }
    public Transform Transform => MainCamera != null ? MainCamera.transform : null;

    public bool Resolve(SceneReferenceService refs)
    {
        MainCamera = null;
        Brain = null;

        if (refs == null) return false;
        if (!refs.TryGetTransform(SceneRefKeys.MainCamera, out var camTransform)) return false;

        MainCamera = camTransform.GetComponent<Camera>();
        return MainCamera != null;
    }

    public void EnableVirtualPipeline(float defaultBlendTime = 0.5f)
    {
        if (MainCamera == null) return;

        GameObject.DontDestroyOnLoad(MainCamera.gameObject);

        Brain = MainCamera.GetComponent<CinemachineBrain>();
        if (Brain == null)
        {
            Brain = MainCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        Brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.FixedUpdate;
        Brain.m_DefaultBlend.m_Time = defaultBlendTime;
    }

    public void Reset()
    {
        MainCamera = null;
        Brain = null;
    }
}
