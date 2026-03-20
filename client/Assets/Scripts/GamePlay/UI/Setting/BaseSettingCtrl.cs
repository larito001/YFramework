using UnityEngine;
using YOTO;

public class BaseSettingCtrl : MonoBehaviour
{
    protected SettingPanel Panel { get; private set; }

    public void Initialize(SettingPanel panel)
    {
        Panel = panel;
        OnInitialize();
    }

    protected virtual void OnInitialize()
    {
    }

    protected T Resolve<T>() where T : class
    {
        return Panel != null ? Panel.Resolve<T>() : null;
    }
}
