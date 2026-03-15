using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public enum UILayerEnum
{
    Normal,
    Top,
    RayCast,
    Tips,
    PopText,
}

public class UILayer
{
    public Dictionary<UIEnum, UIPageHandler> handlers = new Dictionary<UIEnum, UIPageHandler>();
    GameObject uiRoot;
    public GameObject layerRoot;
    private UILayerEnum layer;

    public void Init(GameObject root, UILayerEnum layerEnum)
    {
        layer = layerEnum;
        uiRoot = root;
        layerRoot = new GameObject(layer.ToString());
        layerRoot.layer = LayerMask.NameToLayer("UI");
        layerRoot.transform.SetParent(root.transform, false);

        Canvas canvas = layerRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = GameLoop.Instance.Ctx.Get<CameraMgr>().getMainCamera();
        canvas.overrideSorting = true;
        canvas.sortingOrder = (int)layer * 100;

        CanvasScaler scaler = layerRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0;

        layerRoot.AddComponent<GraphicRaycaster>();
    }

    public void Show(UIInfo info, object param)
    {
        if (!handlers.TryGetValue(info.uiEnum, out UIPageHandler newHandler) || newHandler == null)
        {
            newHandler = new UIPageHandler();
            handlers[info.uiEnum] = newHandler;
        }

        newHandler.Init(info.key, info.uiEnum, param);
        newHandler.SetLoadCallback(() => { });
        newHandler.Load(this);
    }

    public void Hide(UIEnum uiEnum)
    {
        if (handlers.TryGetValue(uiEnum, out UIPageHandler handler))
        {
            handler.OnHide();
        }
    }

    public void Clear()
    {
        foreach (var uiPageHandler in handlers)
        {
            if (uiPageHandler.Key != UIEnum.LoadingPanel)
            {
                uiPageHandler.Value.OnHide();
            }
        }
    }

    public void Resize()
    {
        foreach (var uiLayer in handlers)
        {
            uiLayer.Value.OnResize();
        }
    }
}

public class UIMgr : IGameService
{
    UIConfig uIConfig;
    public GameObject UIRoot;
    private readonly Dictionary<UILayerEnum, UILayer> uiLayers = new Dictionary<UILayerEnum, UILayer>();

    public UIMgr(UIConfig config = null)
    {
        uIConfig = config ?? new UIConfig();
    }

    public UILayer GetLayer(UILayerEnum layerEnum)
    {
        if (uiLayers.ContainsKey(layerEnum))
        {
            return uiLayers[layerEnum];
        }

        return null;
    }

    private void SetUILayer(GameObject obj)
    {
        obj.layer = LayerMask.NameToLayer("UI");
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            SetUILayer(obj.transform.GetChild(i).gameObject);
        }
    }

    public void Show(UIEnum uiEnum, object param = null)
    {
        Debug.Log($"[UIMgr] Show: uiEnum={uiEnum}");
        if (!uIConfig.uiConfigDic.TryGetValue(uiEnum, out UIInfo point))
        {
            Debug.LogWarning($"[UIMgr] Show skipped because '{uiEnum}' is not configured.");
            return;
        }

        if (uiLayers.ContainsKey(point.layer))
        {
            uiLayers[point.layer].Show(point, param);
        }
    }

    public void OnUILoaded(GameObject uiObject)
    {
        if (uiObject != null)
        {
            SetUILayer(uiObject);
        }
    }

    public void Hide(UIEnum uiEnum)
    {
        Debug.Log($"[UIMgr] Hide: uiEnum={uiEnum}");
        if (uIConfig.uiConfigDic.TryGetValue(uiEnum, out UIInfo point))
        {
            if (uiLayers.TryGetValue(point.layer, out UILayer type))
            {
                type?.Hide(uiEnum);
            }
        }
    }

    public void ClearUI()
    {
        Debug.Log("[UIMgr] Clearing all UIs");
        foreach (var typeBase in uiLayers.Values)
        {
            typeBase?.Clear();
        }
    }

    public void ResizeScreen()
    {
    }

    public void Init(GameContext ctx)
    {
        uIConfig.Init();
        UIRoot = new GameObject("UIRoot");
        UIRoot.layer = LayerMask.NameToLayer("UI");
        GameObject.DontDestroyOnLoad(UIRoot);
        foreach (UILayerEnum layer in System.Enum.GetValues(typeof(UILayerEnum)))
        {
            UILayer layertemp = new UILayer();
            layertemp.Init(UIRoot, layer);
            uiLayers.Add(layer, layertemp);
        }
    }

    public void Shutdown()
    {
        ClearUI();
    }
}
