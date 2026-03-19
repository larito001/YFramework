using System.Collections.Generic;
using System;

public class UIInfo
{
    public const float DefaultAutoDestroyDelay = 10f;

    public UIInfo(UIEnum e, UILayerEnum l, string k, float autoDestroyDelay = DefaultAutoDestroyDelay, Type page = null)
    {
        uiEnum = e;
        key = k;
        layer = l;
        closeDestroyDelay = autoDestroyDelay;
        pageType = page;
    }

    public readonly UIEnum uiEnum;
    public readonly string key;
    public readonly UILayerEnum layer;
    public readonly float closeDestroyDelay;
    public readonly Type pageType;

    public override bool Equals(object obj)
    {
        if (!(obj is UIInfo other)) return false;
        return uiEnum == other.uiEnum &&
               layer == other.layer &&
               key == other.key &&
               closeDestroyDelay.Equals(other.closeDestroyDelay) &&
               pageType == other.pageType;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 23 + uiEnum.GetHashCode();
        hash = hash * 23 + layer.GetHashCode();
        hash = hash * 23 + (key != null ? key.GetHashCode() : 0);
        hash = hash * 23 + closeDestroyDelay.GetHashCode();
        hash = hash * 23 + (pageType != null ? pageType.GetHashCode() : 0);
        return hash;
    }
}

public class UIConfig
{
    private readonly List<UIInfo> uiList = new List<UIInfo>();
    private UIInfo loadingInfo;
    private readonly Dictionary<Type, UIInfo> uiConfigByPageType = new Dictionary<Type, UIInfo>();

    public readonly Dictionary<UIEnum, UIInfo> uiConfigDic = new Dictionary<UIEnum, UIInfo>();

    public UIInfo LoadingInfo => loadingInfo;

    public void Register(UIInfo info)
    {
        if (info == null)
        {
            return;
        }

        uiList.Add(info);
    }

    public void Register<TPage>(UIEnum uiEnum, UILayerEnum layer, string key,
        float autoDestroyDelay = UIInfo.DefaultAutoDestroyDelay) where TPage : UIPageBase
    {
        Register(new UIInfo(uiEnum, layer, key, autoDestroyDelay, typeof(TPage)));
    }

    public void RegisterLoading(UIInfo info)
    {
        loadingInfo = info;
        Register(info);
    }

    public void RegisterLoading<TPage>(UIEnum uiEnum, UILayerEnum layer, string key,
        float autoDestroyDelay = UIInfo.DefaultAutoDestroyDelay) where TPage : UIPageBase
    {
        RegisterLoading(new UIInfo(uiEnum, layer, key, autoDestroyDelay, typeof(TPage)));
    }

    public void Init()
    {
        uiConfigDic.Clear();
        uiConfigByPageType.Clear();
        for (var i = 0; i < uiList.Count; i++)
        {
            var config = uiList[i];
            if (uiConfigDic.ContainsKey(config.uiEnum))
            {
                UnityEngine.Debug.LogWarning($"[UIConfig] Duplicate UI enum registration found: {config.uiEnum}. Latest registration will be used.");
            }

            uiConfigDic[config.uiEnum] = config;

            if (config.pageType == null)
            {
                continue;
            }

            if (uiConfigByPageType.ContainsKey(config.pageType))
            {
                UnityEngine.Debug.LogWarning($"[UIConfig] Duplicate UI page registration found: {config.pageType.Name}. Latest registration will be used.");
            }

            uiConfigByPageType[config.pageType] = config;
        }
    }

    public bool TryGet(UIEnum uiEnum, out UIInfo info)
    {
        return uiConfigDic.TryGetValue(uiEnum, out info);
    }

    public bool TryGet(Type pageType, out UIInfo info)
    {
        return uiConfigByPageType.TryGetValue(pageType, out info);
    }

    public bool TryGet<TPage>(out UIInfo info) where TPage : UIPageBase
    {
        return TryGet(typeof(TPage), out info);
    }
}
