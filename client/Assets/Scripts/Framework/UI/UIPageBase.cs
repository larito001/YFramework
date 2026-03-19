using System.Collections.Generic;
using System;
using UnityEngine;

public interface IUIService
{
    void Show(UIEnum uiEnum, object param = null);
    void Show<TPage>(object param = null) where TPage : UIPageBase;
    void Hide(UIEnum uiEnum);
    void Hide<TPage>() where TPage : UIPageBase;
    void ShowLoading(object param = null);
    void HideLoading();
    bool IsShown(UIEnum uiEnum);
    bool TryGetPage<TPage>(out TPage page) where TPage : UIPageBase;
    Transform GetLayerRoot(UILayerEnum layerEnum);
}

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(YOTOUIShow))]
/// <summary>
/// Base class for UI pages managed by <see cref="UIMgr"/>.
/// Dependencies are injected once through <see cref="Initialize"/>.
/// </summary>
public abstract class UIPageBase : MonoBehaviour
{
    private readonly List<YOTOUIChangeBase> tweenList = new List<YOTOUIChangeBase>();
    private IUIService uiService;

    protected GameContext Context { get; private set; }
    protected IUIService UIManager => uiService;

    public UIEnum uiType;
    public CanvasGroup canvasGroup;

    protected virtual void Awake()
    {
        var showTween = GetComponent<YOTOUIShow>();
        if (showTween != null)
        {
            tweenList.Add(showTween);
        }
    }

    public void Initialize(GameContext context, IUIService manager)
    {
        Context = context;
        uiService = manager;
    }

    /// <summary>
    /// Resolve a runtime service from the injected <see cref="GameContext"/>.
    /// </summary>
    protected T GetService<T>() where T : class
    {
        return Context.Get<T>();
    }

    public T Resolve<T>() where T : class
    {
        return Context.Get<T>();
    }

    public abstract void OnLoad();

    public virtual void BeforeShow(object param)
    {
    }

    public abstract void OnShow();
    public abstract void OnHide();
    public abstract void OnResize();

    protected void Show<TPage>() where TPage : UIPageBase
    {
        uiService?.Show<TPage>();
    }

    protected void Show<TPage, TParam>(TParam param) where TPage : UIPageBase
    {
        uiService?.Show<TPage>(param);
    }

    protected void Hide<TPage>() where TPage : UIPageBase
    {
        uiService?.Hide<TPage>();
    }

    public void Enter()
    {
        for (var i = 0; i < tweenList.Count; i++)
        {
            tweenList[i].OnEnter();
        }
    }

    public void Exit()
    {
        for (var i = 0; i < tweenList.Count; i++)
        {
            tweenList[i].OnExist();
        }
    }

    public void CloseSelf()
    {
        uiService?.Hide(uiType);
    }
}

public abstract class UIPageBase<TParam> : UIPageBase
{
    protected TParam PageParam { get; private set; }

    public sealed override void BeforeShow(object param)
    {
        base.BeforeShow(param);

        if (!TryConvertParam(param, out var typedParam))
        {
            Debug.LogError($"[UIPageBase] Invalid page param for {GetType().Name}. Expected={typeof(TParam).Name}, Actual={(param == null ? "null" : param.GetType().Name)}");
            return;
        }

        PageParam = typedParam;
        OnBeforeShow(typedParam);
    }

    protected virtual void OnBeforeShow(TParam param)
    {
    }

    private static bool TryConvertParam(object param, out TParam typedParam)
    {
        if (param is TParam converted)
        {
            typedParam = converted;
            return true;
        }

        if (param == null)
        {
            typedParam = default;
            return !typeof(TParam).IsValueType || Nullable.GetUnderlyingType(typeof(TParam)) != null;
        }

        typedParam = default;
        return false;
    }
}
