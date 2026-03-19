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
    public readonly Dictionary<UIEnum, UIPageHandler> handlers = new Dictionary<UIEnum, UIPageHandler>();
    public GameObject layerRoot;

    private readonly UIMgr uiMgr;
    private readonly ResMgr resMgr;
    private readonly GameContext context;
    private readonly Camera mainCamera;
    private readonly UILayerEnum layer;

    public UILayer(UIMgr manager, ResMgr resourceManager, GameContext gameContext, UILayerEnum layerEnum, Camera camera)
    {
        uiMgr = manager;
        resMgr = resourceManager;
        context = gameContext;
        mainCamera = camera;
        layer = layerEnum;
    }

    public void Init(GameObject root)
    {
        layerRoot = new GameObject(layer.ToString());
        layerRoot.layer = LayerMask.NameToLayer("UI");
        layerRoot.transform.SetParent(root.transform, false);

        Canvas canvas = layerRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = mainCamera;
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
        if (!handlers.TryGetValue(info.uiEnum, out UIPageHandler handler) || handler == null)
        {
            handler = new UIPageHandler(uiMgr, resMgr, context);
            handlers[info.uiEnum] = handler;
        }

        handler.Init(info.key, info.uiEnum, param);
        handler.SetLoadCallback(() => { });
        handler.Load(this);
    }

    public void Hide(UIEnum uiEnum)
    {
        if (handlers.TryGetValue(uiEnum, out UIPageHandler handler))
        {
            handler.OnHide();
        }
    }

    public void Clear(bool destroy)
    {
        foreach (var uiPageHandler in handlers)
        {
            if (uiPageHandler.Key != UIEnum.LoadingPanel)
            {
                if (destroy)
                {
                    uiPageHandler.Value.Destroy();
                }
                else
                {
                    uiPageHandler.Value.OnHide();
                }
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
    private readonly UIConfig uiConfig;
    private readonly Dictionary<UILayerEnum, UILayer> uiLayers = new Dictionary<UILayerEnum, UILayer>();

    private GameContext context;
    private ResMgr resMgr;
    private CameraMgr cameraMgr;

    public GameObject UIRoot { get; private set; }

    public UIMgr(UIConfig config = null)
    {
        uiConfig = config ?? new UIConfig();
    }

    public UILayer GetLayer(UILayerEnum layerEnum)
    {
        if (uiLayers.ContainsKey(layerEnum))
        {
            return uiLayers[layerEnum];
        }

        return null;
    }

    public void Show(UIEnum uiEnum, object param = null)
    {
        Debug.Log($"[UIMgr] Show: uiEnum={uiEnum}");
        if (!uiConfig.uiConfigDic.TryGetValue(uiEnum, out UIInfo point))
        {
            Debug.LogWarning($"[UIMgr] Show skipped because '{uiEnum}' is not configured.");
            return;
        }

        if (uiLayers.TryGetValue(point.layer, out var layer))
        {
            layer.Show(point, param);
        }
    }

    public void OnUILoaded(GameObject uiObject)
    {
        if (uiObject != null)
        {
            SetUILayerRecursively(uiObject);
        }
    }

    public void Hide(UIEnum uiEnum)
    {
        Debug.Log($"[UIMgr] Hide: uiEnum={uiEnum}");
        if (uiConfig.uiConfigDic.TryGetValue(uiEnum, out UIInfo point) &&
            uiLayers.TryGetValue(point.layer, out var layer))
        {
            layer.Hide(uiEnum);
        }
    }

    public void ClearUI()
    {
        Debug.Log("[UIMgr] Clearing all UIs");
        foreach (var layer in uiLayers.Values)
        {
            layer?.Clear(false);
        }
    }

    public void ResizeScreen()
    {
        foreach (var layer in uiLayers.Values)
        {
            layer.Resize();
        }
    }

    public void Init(GameContext ctx)
    {
        context = ctx;
        resMgr = ctx.Get<ResMgr>();
        cameraMgr = ctx.Get<CameraMgr>();
        uiConfig.Init();

        UIRoot = new GameObject("UIRoot");
        UIRoot.layer = LayerMask.NameToLayer("UI");
        GameObject.DontDestroyOnLoad(UIRoot);

        foreach (UILayerEnum layerEnum in System.Enum.GetValues(typeof(UILayerEnum)))
        {
            var layer = new UILayer(this, resMgr, context, layerEnum, cameraMgr.getMainCamera());
            layer.Init(UIRoot);
            uiLayers.Add(layerEnum, layer);
        }
    }

    public void Shutdown()
    {
        foreach (var layer in uiLayers.Values)
        {
            layer?.Clear(true);
        }

        if (UIRoot != null)
        {
            Object.Destroy(UIRoot);
            UIRoot = null;
        }

        uiLayers.Clear();
    }

    private void SetUILayerRecursively(GameObject obj)
    {
        obj.layer = LayerMask.NameToLayer("UI");
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            SetUILayerRecursively(obj.transform.GetChild(i).gameObject);
        }
    }
}
