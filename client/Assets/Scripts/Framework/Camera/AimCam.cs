using Cinemachine;
using UnityEngine;

/// <summary>
/// 瞄准相机包装：从场景按名字解析 CinemachineVirtualCamera，提供 Enter/Exit 切换。
/// Enter 时以 ActivePriority 覆盖 FreeLookCam，Exit 时回落到 InactivePriority 让 FreeLookCam 复位。
/// </summary>
public class AimCam
{
    public const int ActivePriority = 20;
    public const int InactivePriority = 0;

    private CinemachineVirtualCamera vcam;

    public CinemachineVirtualCamera VCam => vcam;
    public bool IsAvailable => vcam != null;
    public bool IsActive { get; private set; }

    public bool Resolve(SceneReferenceService refs, string key = null)
    {
        vcam = null;
        IsActive = false;
        if (refs == null) return false;

        string targetKey = string.IsNullOrEmpty(key) ? SceneRefKeys.MainCameraAim : key;
        if (!refs.TryGetTransform(targetKey, out var t)) return false;

        vcam = t.GetComponent<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.m_Priority = InactivePriority;
        }
        return vcam != null;
    }

    public void SetTarget(Transform follow, Transform lookAt = null)
    {
        if (vcam == null) return;
        vcam.Follow = follow;
        vcam.LookAt = lookAt != null ? lookAt : follow;
    }

    public void Enter()
    {
        if (vcam == null) return;
        vcam.m_Priority = ActivePriority;
        IsActive = true;
    }

    public void Exit()
    {
        if (vcam == null) return;
        vcam.m_Priority = InactivePriority;
        IsActive = false;
    }

    public void Reset()
    {
        if (vcam != null) vcam.m_Priority = InactivePriority;
        vcam = null;
        IsActive = false;
    }
}
