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
    private Action _onEnterSceneComplete;
    private Action<bool> _onLeaveSceneComplete;
    // 场景资源持有对象
    public GameObject ResHandlerObj;


    #region 重写

    public abstract GotSceneType SceneType { get; }

    public abstract string SceneName { get; }

    public virtual int LoadFileTotal
    {
        get { return 1000; }
    }

    /// <summary>
    /// 场景注册时调用
    /// </summary>
    protected virtual void OnCreate()
    {
    }

    /// <summary>
    /// 进入场景，开始加载资源
    /// </summary>
    protected virtual void OnEnterScene()
    {
        //todo:重写后添加逻辑，不带base，后续手动调用EnterSceneComplete
        EnterSceneComplete();
    }

    /// <summary>
    /// 加载完成后回调，可以理解为EnterSceneComplete调完之后立马调这个
    /// </summary>
    protected virtual void OnLoadingEnd()
    {
    }

    /// <summary>
    /// 离开场景，开始卸载资源
    /// </summary>
    protected virtual void OnLeaveScene()
    {
        //todo:重写后添加逻辑，不带base，后续手动调用LeaveSceneComplete
        LeaveSceneComplete();
    }

    #endregion

    #region 逻辑

    public void InitController()
    {
        OnCreate();
    }

    public void EnterScene(Action onComplete, LoadSceneMode loadSceneMode)
    {
        _onEnterSceneComplete = onComplete;

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

        if (_onEnterSceneComplete == null)
        {
            return;
        }

        _onEnterSceneComplete();
        _onEnterSceneComplete = null;
    }

    public void LoadingEnd()
    {
        Debug.Log(SceneName+"场景加载完成");
        OnLoadingEnd();
    }

    public void LeaveScene(Action<bool> onComplete)
    {
        _onLeaveSceneComplete = onComplete;

        OnLeaveScene();
        rootObj.SetActive(false);
    }


    protected void LeaveSceneComplete()
    {
        if (_onLeaveSceneComplete == null)
        {
            return;
        }

        _onLeaveSceneComplete(true);
        _onLeaveSceneComplete = null;
    }


    public void initResHandlerObj()
    {
        if (ResHandlerObj == null)
        {
            ResHandlerObj = new GameObject();
        }

        ResHandlerObj.name = SceneName;
    }

    public void DestoryResHandlerObj()
    {
        if (ResHandlerObj != null)
        {
            GameObject.DestroyImmediate(ResHandlerObj);
            Debug.Assert(ResHandlerObj == null);
        }
    }

    #endregion
}