using System.Collections.Generic;

public class UIInfo
{
    public UIInfo(UIEnum e, UILayerEnum l, string k)
    {
        uiEnum = e;
        key = k;
        layer = l;
    }

    public UIEnum uiEnum;
    public string key;
    public UILayerEnum layer;

    public override bool Equals(object obj)
    {
        if (!(obj is UIInfo other)) return false;
        return uiEnum == other.uiEnum &&
               layer == other.layer &&
               key == other.key;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 23 + uiEnum.GetHashCode();
        hash = hash * 23 + layer.GetHashCode();
        hash = hash * 23 + (key != null ? key.GetHashCode() : 0);
        return hash;
    }
}

public class UIConfig
{
    private readonly List<UIInfo> uiList = new List<UIInfo>();

    public readonly Dictionary<UIEnum, UIInfo> uiConfigDic = new Dictionary<UIEnum, UIInfo>();

    public void Register(UIInfo info)
    {
        if (info == null)
        {
            return;
        }

        uiList.Add(info);
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
