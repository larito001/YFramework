using System.Collections.Generic;

public class UIInfo
{
    public const float DefaultAutoDestroyDelay = 10f;

    public UIInfo(UIEnum e, UILayerEnum l, string k, float autoDestroyDelay = DefaultAutoDestroyDelay)
    {
        uiEnum = e;
        key = k;
        layer = l;
        closeDestroyDelay = autoDestroyDelay;
    }

    public UIEnum uiEnum;
    public string key;
    public UILayerEnum layer;
    public float closeDestroyDelay;

    public override bool Equals(object obj)
    {
        if (!(obj is UIInfo other)) return false;
        return uiEnum == other.uiEnum &&
               layer == other.layer &&
               key == other.key &&
               closeDestroyDelay.Equals(other.closeDestroyDelay);
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 23 + uiEnum.GetHashCode();
        hash = hash * 23 + layer.GetHashCode();
        hash = hash * 23 + (key != null ? key.GetHashCode() : 0);
        hash = hash * 23 + closeDestroyDelay.GetHashCode();
        return hash;
    }
}

public class UIConfig
{
    private readonly List<UIInfo> uiList = new List<UIInfo>();
    private UIInfo loadingInfo;

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

    public void RegisterLoading(UIInfo info)
    {
        loadingInfo = info;
        Register(info);
    }

    public void Init()
    {
        uiConfigDic.Clear();
        for (var i = 0; i < uiList.Count; i++)
        {
            var config = uiList[i];
            uiConfigDic[config.uiEnum] = config;
        }
    }
}
