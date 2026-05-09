using System;
using UnityEngine;

/// <summary>
/// Base scene flow object used by <see cref="YSceneManager"/>.
/// Scene instances own their enter and leave hooks and can resolve services
/// from the injected <see cref="GameContext"/>.
/// </summary>
public abstract class YSceneBase
{
    public object SceneArgs { get; set; }
    public GameObject rootObj;
    public Transform rootTrn;
    public GameObject ResHandlerObj;

    protected GameContext Context { get; private set; }

    private Action onEnterSceneComplete;
    private Action<bool> onLeaveSceneComplete;

    public abstract YSceneType SceneType { get; }
    public abstract string SceneName { get; }

    public virtual int LoadFileTotal => 1000;

    public void Initialize(GameContext context)
    {
        Context = context;
    }

    protected T GetService<T>() where T : class
    {
        return Context.Get<T>();
    }

    protected IUIService UI => GetService<UIMgr>();

    protected virtual void OnCreate()
    {
    }

    protected virtual void OnEnterScene()
    {
        EnterSceneComplete();
    }

    protected virtual void OnLoadingEnd()
    {
    }

    protected virtual void OnLeaveScene()
    {
        LeaveSceneComplete();
    }

    public void InitController()
    {
        OnCreate();
    }

    public void EnterScene(Action onComplete, LoadSceneMode loadSceneMode)
    {
        onEnterSceneComplete = onComplete;

        Application.backgroundLoadingPriority = YSceneManager.LoadingBackgroundLoadingPriority;
        QualitySettings.asyncUploadBufferSize = YSceneManager.LoadingAsyncUploadBufferSize;
        QualitySettings.asyncUploadTimeSlice = YSceneManager.LoadingAsyncUploadTimeSize;
        rootObj.SetActive(true);
        OnEnterScene();
    }

    protected void EnterSceneComplete()
    {
        Application.backgroundLoadingPriority = YSceneManager.DefaultBackgroundLoadingPriority;
        QualitySettings.asyncUploadBufferSize = YSceneManager.DefaultAsyncUploadBufferSize;
        QualitySettings.asyncUploadTimeSlice = YSceneManager.DefaultAsyncUploadTimeSlice;

        onEnterSceneComplete?.Invoke();
        onEnterSceneComplete = null;
    }

    public void LoadingEnd()
    {
        Debug.Log($"{SceneName} loaded.");
        OnLoadingEnd();
    }

    public void LeaveScene(Action<bool> onComplete)
    {
        onLeaveSceneComplete = onComplete;
        OnLeaveScene();
        rootObj.SetActive(false);
    }

    protected void LeaveSceneComplete()
    {
        onLeaveSceneComplete?.Invoke(true);
        onLeaveSceneComplete = null;
    }

    public void InitResHandlerObj()
    {
        if (ResHandlerObj == null)
        {
            ResHandlerObj = new GameObject(SceneName);
            return;
        }

        ResHandlerObj.name = SceneName;
    }

    public void DestroyResHandlerObj()
    {
        if (ResHandlerObj != null)
        {
            GameObject.DestroyImmediate(ResHandlerObj);
            ResHandlerObj = null;
        }
    }
}
