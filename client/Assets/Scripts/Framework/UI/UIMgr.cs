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

        handler.Init(info.key, info.uiEnum, param, info.closeDestroyDelay, info.pageType);
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

    public void Resize()
    {
        foreach (var uiLayer in handlers)
        {
            uiLayer.Value.OnResize();
        }
    }

    public bool TryGetPage<TPage>(UIEnum uiEnum, out TPage page) where TPage : UIPageBase
    {
        if (handlers.TryGetValue(uiEnum, out var handler) && handler.Page is TPage typedPage)
        {
            page = typedPage;
            return true;
        }

        page = null;
        return false;
    }
}

public class UIMgr : IGameService, IUIService
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
        if (!uiConfig.TryGet(uiEnum, out UIInfo point))
        {
            Debug.LogWarning($"[UIMgr] Show skipped because '{uiEnum}' is not configured.");
            return;
        }

        if (uiLayers.TryGetValue(point.layer, out var layer))
        {
            layer.Show(point, param);
        }
    }

    public void Show<TPage>(object param = null) where TPage : UIPageBase
    {
        if (!uiConfig.TryGet<TPage>(out var info))
        {
            Debug.LogWarning($"[UIMgr] Show skipped because page '{typeof(TPage).Name}' is not configured.");
            return;
        }

        Show(info.uiEnum, param);
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
        if (uiConfig.TryGet(uiEnum, out UIInfo point) &&
            uiLayers.TryGetValue(point.layer, out var layer))
        {
            layer.Hide(uiEnum);
        }
    }

    public void Hide<TPage>() where TPage : UIPageBase
    {
        if (!uiConfig.TryGet<TPage>(out var info))
        {
            Debug.LogWarning($"[UIMgr] Hide skipped because page '{typeof(TPage).Name}' is not configured.");
            return;
        }

        Hide(info.uiEnum);
    }

    public void ShowLoading(object param = null)
    {
        if (uiConfig.LoadingInfo == null)
        {
            return;
        }

        Show(uiConfig.LoadingInfo.uiEnum, param);
    }

    public void HideLoading()
    {
        if (uiConfig.LoadingInfo == null)
        {
            return;
        }

        Hide(uiConfig.LoadingInfo.uiEnum);
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

    public bool IsShown(UIEnum uiEnum)
    {
        if (!uiConfig.TryGet(uiEnum, out var info) ||
            !uiLayers.TryGetValue(info.layer, out var layer) ||
            !layer.handlers.TryGetValue(uiEnum, out var handler))
        {
            return false;
        }

        return handler.CurrentState == PageState.Shown;
    }

    public bool TryGetPage<TPage>(out TPage page) where TPage : UIPageBase
    {
        page = null;
        if (!uiConfig.TryGet<TPage>(out var info))
        {
            return false;
        }

        return uiLayers.TryGetValue(info.layer, out var layer) && layer.TryGetPage(info.uiEnum, out page);
    }

    public Transform GetLayerRoot(UILayerEnum layerEnum)
    {
        return GetLayer(layerEnum)?.layerRoot?.transform;
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

        InjectSceneModels();
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

    public void InjectSceneModels()
    {
        var models = Object.FindObjectsOfType<SceneModelBase>(true);
        for (int i = 0; i < models.Length; i++)
        {
            models[i].Inject(this);
        }
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
