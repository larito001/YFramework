using System;
using System.Collections;
using UnityEngine;
using YOTO;

public enum PageState
{
    Unloaded,
    Loading,
    Shown,
    Hidden,
}

public class UIPageHandler
{
    private readonly UIMgr uiMgr;
    private readonly ResMgr resMgr;
    private readonly GameContext context;

    private UIPageBase page;
    private ResourceHandle<GameObject> prefabHandle;
    private UILayer layer;
    private string resourceKey;
    private float closeDestroyDelay;
    private Type expectedPageType;
    private Action onLoadComplete;
    private UIEnum uiType;
    private object param;
    private bool shouldStayHidden;
    private readonly ICoroutineRunner coroutineRunner;
    private Coroutine pendingDestroyCoroutine;

    public PageState CurrentState { get; private set; } = PageState.Unloaded;
    public UIPageBase Page => page;

    public UIPageHandler(UIMgr manager, ResMgr resourceManager, GameContext gameContext)
    {
        uiMgr = manager;
        resMgr = resourceManager;
        context = gameContext;
        coroutineRunner = gameContext.Get<ICoroutineRunner>();
    }

    public void Init(string key, UIEnum type, object showParam, float autoDestroyDelay, Type pageType)
    {
        resourceKey = key;
        uiType = type;
        param = showParam;
        closeDestroyDelay = autoDestroyDelay;
        expectedPageType = pageType;
        shouldStayHidden = false;
        CancelPendingDestroy();
    }

    public void OnResize()
    {
        page?.OnResize();
    }

    public void SetLoadCallback(Action callback)
    {
        onLoadComplete = callback;
    }

    public void Load(UILayer uiLayer)
    {
        layer = uiLayer;

        if (HasInstantiatedPage())
        {
            CancelPendingDestroy();
            page.transform.SetParent(layer.layerRoot.transform, false);
            onLoadComplete?.Invoke();
            onLoadComplete = null;
            Show();
            return;
        }

        if (CurrentState == PageState.Loading)
        {
            return;
        }

        CurrentState = PageState.Loading;
        resMgr.LoadHandleAsync<GameObject>(resourceKey, OnLoaded);
    }

    public void OnHide()
    {
        shouldStayHidden = true;
        if (!HasInstantiatedPage() || CurrentState == PageState.Hidden)
        {
            ScheduleDestroyIfNeeded();
            return;
        }

        page.Exit();
        page.OnHide();
        CurrentState = PageState.Hidden;
        ScheduleDestroyIfNeeded();
    }

    public void Destroy()
    {
        CancelPendingDestroy();
        shouldStayHidden = true;
        if (!HasInstantiatedPage())
        {
            ReleasePrefabHandle();
            return;
        }

        page.Exit();
        page.OnHide();
        var pageObject = page.gameObject;
        page = null;
        CurrentState = PageState.Unloaded;
        UnityEngine.Object.Destroy(pageObject);
        ReleasePrefabHandle();
    }

    private void OnLoaded(ResourceHandle<GameObject> handle)
    {
        prefabHandle = handle;
        var prefab = handle?.Asset;
        if (prefab == null)
        {
            Debug.LogError($"[UIPageHandler] Failed to load UI prefab: key={resourceKey}");
            CurrentState = PageState.Unloaded;
            ReleasePrefabHandle();
            return;
        }

        try
        {
            var pageObject = UnityEngine.Object.Instantiate(prefab, layer.layerRoot.transform);
            var pageComponent = pageObject.GetComponent<UIPageBase>();
            if (pageComponent == null)
            {
                Debug.LogError($"[UIPageHandler] UI prefab does not contain UIPageBase: key={resourceKey}");
                CurrentState = PageState.Unloaded;
                UnityEngine.Object.Destroy(pageObject);
                ReleasePrefabHandle();
                return;
            }

            if (expectedPageType != null && !expectedPageType.IsInstanceOfType(pageComponent))
            {
                Debug.LogError($"[UIPageHandler] UI prefab page type mismatch: key={resourceKey}, expected={expectedPageType.Name}, actual={pageComponent.GetType().Name}");
                CurrentState = PageState.Unloaded;
                UnityEngine.Object.Destroy(pageObject);
                ReleasePrefabHandle();
                return;
            }

            page = pageComponent;
            page.canvasGroup = page.GetComponent<CanvasGroup>();
            if (page.canvasGroup == null)
            {
                Debug.LogError($"[UIPageHandler] UI prefab does not contain CanvasGroup: key={resourceKey}");
                var invalidPageObject = page.gameObject;
                page = null;
                CurrentState = PageState.Unloaded;
                UnityEngine.Object.Destroy(invalidPageObject);
                ReleasePrefabHandle();
                return;
            }

            page.Initialize(context, uiMgr);
            page.uiType = uiType;
            page.Exit();
            page.OnLoad();
            uiMgr.OnUILoaded(page.gameObject);

            onLoadComplete?.Invoke();
            onLoadComplete = null;

            if (shouldStayHidden)
            {
                CurrentState = PageState.Hidden;
                ScheduleDestroyIfNeeded();
                return;
            }

            Show();
        }
        catch (Exception e)
        {
            CurrentState = PageState.Unloaded;
            Debug.LogError($"[UIPageHandler] Exception while creating UI '{resourceKey}': {e}");
            if (page != null && page.gameObject != null)
            {
                UnityEngine.Object.Destroy(page.gameObject);
                page = null;
            }

            ReleasePrefabHandle();
        }
    }

    private void Show()
    {
        if (!HasInstantiatedPage() || page.canvasGroup == null)
        {
            Debug.LogError($"[UIPageHandler] Failed to show UI: key={resourceKey}");
            return;
        }

        page.Enter();
        page.BeforeShow(param);
        // 先把状态置为 Shown，再回调 OnShow()。
        // 否则若面板在 OnShow() 里同步关掉自己(如 LeaderboardPanel 调起渠道原生榜单后 CloseSelf)，
        // 其 Hide 把状态置成 Hidden，却被这里随后的 shouldStayHidden=false / CurrentState=Shown 覆盖回去，
        // 面板就永久卡在 Shown——进而被 CombatInputGate 当成"有面板开着"而屏蔽战斗输入(滑屏环视失灵)。
        shouldStayHidden = false;
        CancelPendingDestroy();
        CurrentState = PageState.Shown;
        page.OnShow();
    }

    private bool HasInstantiatedPage()
    {
        return page != null && page.gameObject != null;
    }

    private void ScheduleDestroyIfNeeded()
    {
        CancelPendingDestroy();
        if (closeDestroyDelay <= 0f || CurrentState != PageState.Hidden)
        {
            return;
        }

        pendingDestroyCoroutine = coroutineRunner.Run(DestroyAfterDelay());
    }

    private void CancelPendingDestroy()
    {
        if (pendingDestroyCoroutine == null)
        {
            return;
        }

        coroutineRunner.Stop(pendingDestroyCoroutine);
        pendingDestroyCoroutine = null;
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(closeDestroyDelay);
        pendingDestroyCoroutine = null;

        if (CurrentState == PageState.Hidden && shouldStayHidden)
        {
            Destroy();
        }
    }

    private void ReleasePrefabHandle()
    {
        prefabHandle?.Release();
        prefabHandle = null;
    }
}
