using System;
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
    private UIPageBase page;
    private UILayer layer;
    private string resourceKey;
    private Action onLoadComplete;
    private UIEnum uiType;
    private object param;
    private bool shouldStayHidden;

    public PageState CurrentState { get; private set; } = PageState.Unloaded;

    public void Init(string key, UIEnum type, object showParam)
    {
        resourceKey = key;
        uiType = type;
        param = showParam;
        shouldStayHidden = false;
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
        GameLoop.Instance.Ctx.Get<ResMgr>().LoadUI(resourceKey, OnLoaded);
    }

    public void OnHide()
    {
        shouldStayHidden = true;
        if (!HasInstantiatedPage() || CurrentState == PageState.Hidden)
        {
            return;
        }

        page.Exit();
        page.OnHide();
        CurrentState = PageState.Hidden;
    }

    public void Destroy()
    {
        OnHide();
    }

    private void OnLoaded(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"[UIPageHandler] Failed to load UI prefab: key={resourceKey}");
            CurrentState = PageState.Unloaded;
            return;
        }

        try
        {
            var pageComponent = UnityEngine.Object.Instantiate(prefab, layer.layerRoot.transform).GetComponent<UIPageBase>();
            if (pageComponent == null)
            {
                Debug.LogError($"[UIPageHandler] UI prefab does not contain UIPageBase: key={resourceKey}");
                CurrentState = PageState.Unloaded;
                return;
            }

            page = pageComponent;
            page.canvasGroup = page.GetComponent<CanvasGroup>();
            if (page.canvasGroup == null)
            {
                Debug.LogError($"[UIPageHandler] UI prefab does not contain CanvasGroup: key={resourceKey}");
                CurrentState = PageState.Unloaded;
                return;
            }

            page.uiType = uiType;
            page.Exit();
            page.OnLoad();
            GameLoop.Instance.Ctx.Get<UIMgr>().OnUILoaded(page.gameObject);

            onLoadComplete?.Invoke();
            onLoadComplete = null;

            if (shouldStayHidden)
            {
                CurrentState = PageState.Hidden;
                return;
            }

            Show();
        }
        catch (Exception e)
        {
            CurrentState = PageState.Unloaded;
            Debug.LogError($"[UIPageHandler] Exception while creating UI '{resourceKey}': {e}");
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
        page.OnShow();
        shouldStayHidden = false;
        CurrentState = PageState.Shown;
    }

    private bool HasInstantiatedPage()
    {
        return page != null && page.gameObject != null;
    }
}
