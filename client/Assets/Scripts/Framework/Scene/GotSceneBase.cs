using System;
using UnityEngine;

public class TestScene : GotSceneBase
{
    public override GotSceneType SceneType { get; }
    public override string SceneName { get; }
}

public abstract class GotSceneBase
{
    public object SceneArgs { get; set; }
    public GameObject rootObj;
    public Transform rootTrn;
    public GameObject ResHandlerObj;

    private Action onEnterSceneComplete;
    private Action<bool> onLeaveSceneComplete;

    public abstract GotSceneType SceneType { get; }
    public abstract string SceneName { get; }

    public virtual int LoadFileTotal => 1000;

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

        Application.backgroundLoadingPriority = GotSceneManager.LoadingBackgroundLoadingPriority;
        QualitySettings.asyncUploadBufferSize = GotSceneManager.LoadingAsyncUploadBufferSize;
        QualitySettings.asyncUploadTimeSlice = GotSceneManager.LoadingAsyncUploadTimeSize;
        rootObj.SetActive(true);
        OnEnterScene();
    }

    protected void EnterSceneComplete()
    {
        Application.backgroundLoadingPriority = GotSceneManager.DefaultBackgroundLoadingPriority;
        QualitySettings.asyncUploadBufferSize = GotSceneManager.DefaultAsyncUploadBufferSize;
        QualitySettings.asyncUploadTimeSlice = GotSceneManager.DefaultAsyncUploadTimeSlice;

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
