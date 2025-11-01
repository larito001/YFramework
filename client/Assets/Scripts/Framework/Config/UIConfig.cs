using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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
        if (!(obj is UIInfo)) return false;
        UIInfo other = (UIInfo)obj;
        return layer == other.layer &&
               key == other.key &&
               layer == other.layer;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 23 + layer.GetHashCode();
        hash = hash * 23 + (key != null ? key.GetHashCode() : 0);
        hash = hash * 23 + layer.GetHashCode();
        return hash;
    }
}

public enum UIEnum
{
    None = 0,
    LoadingPanel,
    StartPanel,
    GameMainPanel,
    FinishPanel,
    SettingPanel,
}

public class UIConfig
{
    private List<UIInfo> uiList = new List<UIInfo>()
    {
        new UIInfo(UIEnum.StartPanel, UILayerEnum.Normal, "UI/StartPanel"),
        new UIInfo(UIEnum.GameMainPanel, UILayerEnum.Normal, "UI/GameMainPanel"),
        new UIInfo(UIEnum.LoadingPanel, UILayerEnum.RayCast, "UI/LoadingPanel"),
        new UIInfo(UIEnum.FinishPanel, UILayerEnum.Normal, "UI/FinishPanel"),
        new UIInfo(UIEnum.SettingPanel, UILayerEnum.Normal, "UI/Setting/SettingPanel"),
    };

    #region  对外

    public readonly Dictionary<UIEnum, UIInfo> uiConfigDic = new Dictionary<UIEnum, UIInfo>();

    public void Init()
    {
        uiConfigDic.Clear();
        for (var i = 0; i < uiList.Count; i++)
        {
            var config = uiList[i];
            uiConfigDic.Add(config.uiEnum, config);
        }
    }

    #endregion
 
}