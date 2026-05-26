using Cinemachine;
using UnityEngine;

/// <summary>
/// FreeLook 自由视角相机包装：从场景按名字解析 CinemachineFreeLook，提供目标绑定与优先级控制。
/// 作为基础视角，优先级保持在 BasePriority；AimCam 进入时会以更高优先级覆盖之。
/// </summary>
public class FreeLookCam
{
    public const int BasePriority = 10;

    private CinemachineFreeLook vcam;

    public CinemachineFreeLook VCam => vcam;
    public bool IsAvailable => vcam != null;

    public bool Resolve(SceneReferenceService refs, string key = null)
    {
        vcam = null;
        if (refs == null) return false;

        string targetKey = string.IsNullOrEmpty(key) ? SceneRefKeys.MainCameraFreeLook : key;
        if (!refs.TryGetTransform(targetKey, out var t)) return false;

        vcam = t.GetComponent<CinemachineFreeLook>();
        if (vcam != null)
        {
            vcam.m_Priority = BasePriority;
        }
        return vcam != null;
    }

    public void SetTarget(Transform follow, Transform lookAt = null)
    {
        if (vcam == null) return;
        vcam.Follow = follow;
        vcam.LookAt = lookAt != null ? lookAt : follow;
    }

    public void Activate()
    {
        if (vcam == null) return;
        vcam.m_Priority = BasePriority;
    }

    public void Reset()
    {
        vcam = null;
    }
}
