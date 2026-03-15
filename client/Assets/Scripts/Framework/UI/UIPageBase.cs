using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(YOTOUIShow))]
/// <summary>
/// Base class for UI pages managed by <see cref="UIMgr"/>.
/// Dependencies are injected once through <see cref="Initialize"/>.
/// </summary>
public abstract class UIPageBase : MonoBehaviour
{
    private readonly List<YOTOUIChangeBase> tweenList = new List<YOTOUIChangeBase>();
    private UIMgr uiMgr;

    protected GameContext Context { get; private set; }
    protected UIMgr UIManager => uiMgr;

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

    public void Initialize(GameContext context, UIMgr manager)
    {
        Context = context;
        uiMgr = manager;
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
        uiMgr?.Hide(uiType);
    }
}
