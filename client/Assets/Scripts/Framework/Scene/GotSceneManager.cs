using System;
using System.Collections.Generic;
using UnityEngine;
using YOTO;


public enum GotSceneType
{
    Home, // 登陆场景(空场景)
    GamePlay, //野外
    None,
}

public enum LoadSceneMode
{
    //单场景加载
    Single = 0,

    //多场景附加
    Additive = 1,
}

public class GotSceneManager:IGameService
{
    //场景总Gameobject
    public GameObject SceneRoot { get; private set; }

    //注册已经注册的场景
    private readonly Dictionary<GotSceneType, GotSceneBase> m_scenes = new Dictionary<GotSceneType, GotSceneBase>();
    //当前场景
    public GotSceneBase CurrentScene { get; set; }
    //当前场景的类型
    public GotSceneType SceneType
    {
        get { return CurrentScene == null ? GotSceneType.None : CurrentScene.SceneType; }
    }
    //加载完成的场景
    private Stack<GotSceneType> m_loadedScenes;
    //正在加载的场景的加载模式
    private LoadSceneMode m_loadSceneMode = LoadSceneMode.Single;
    //上一个场景的类型
    public GotSceneType PreSceneType;
    //是否正在切换场景
    public bool SwitchSceneComplete = false;

    //加载配置
    public static ThreadPriority LoadingBackgroundLoadingPriority = ThreadPriority.High;
    public static int LoadingAsyncUploadBufferSize = 4;
    public static int LoadingAsyncUploadTimeSize = 33;

    public static ThreadPriority DefaultBackgroundLoadingPriority;
    public static int DefaultAsyncUploadBufferSize;
    public static int DefaultAsyncUploadTimeSlice;


    /// <summary>
    /// 添加场景
    /// </summary>
    /// <param name="sceneRoot"></param>
    /// <typeparam name="T"></typeparam>
    private void AddScene<T>(GameObject sceneRoot) where T : GotSceneBase, new()
    {
        GotSceneBase scene = new T();
        m_scenes[scene.SceneType] = scene;

        GameObject obj = new GameObject(scene.SceneName);
        obj.transform.parent = sceneRoot.transform;
        obj.SetActive(false);

        scene.rootObj = obj;
        scene.rootTrn = obj.transform;
        scene.InitController();
    }


    #region 生命周期
    


    /// <summary>
    /// 游戏场景切换, 可以存在多个场景  
    /// Single模式则和之前一样，只剩下一个要切换的场景
    /// Additive模式 是场景叠加的模式，显示一个场景，其他场景隐藏，，需要各种主场景实现隐藏和显示的接口 OnEnterScene，OnShowScene    
    /// Additive模式 默认 不释放任何资源，显示的时候 对应也不用加载任何资源
    /// </summary>
    public void SwitchScene(GotSceneType sceneType, object args = null, bool showLoading = true,
        LoadSceneMode loadSceneMode = LoadSceneMode.Single)
    {
        Debug.Assert(m_loadedScenes.Count <= 1); // 有叠加场景时需先Unload

        var comingScene = GetScene(sceneType);
        if (null == comingScene)
            return;
        
        int loadingTipsType = 2;
        if (CurrentScene != null && CurrentScene.SceneType != sceneType)
        {
            PreSceneType = SceneType;
        }

        if (CurrentScene != null && CurrentScene.SceneType == sceneType)
        {
            return;
        }

        SwitchSceneComplete = false;
        m_loadSceneMode = loadSceneMode;
        var leavingScene = CurrentScene;
        CurrentScene = comingScene;
        CurrentScene.SceneArgs = args;
        if (loadSceneMode == LoadSceneMode.Single && leavingScene != null)
        {
            leavingScene.DestoryResHandlerObj();
        }

        CurrentScene.initResHandlerObj();


        Debug.LogFormat("RES: GotSceneManager::SwitchScene(sceneType = {0})", CurrentScene.SceneName);


        if (showLoading)
        {
            GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.LoadingPanel);
        }

        //第一次切换出主城的时候引起卡顿，目前原因未知，临时延时执行后续操作保证先出Loading界面
        //卸载过程中,资源开始加载,导致资源要使用的资源被卸载掉。yangyong
        if (leavingScene == null)
        {
            LeaveSceneCompleteAndStartGC(OnGCEndAndEnterScene, false);
        }
        else if (m_loadSceneMode == LoadSceneMode.Additive)
        {
            //叠加场景不需要leavescene
            LeaveSceneCompleteAndStartGC(OnGCEndAndEnterScene);
        }
        else
        {
            //调用场景的leaveScene方法
            leavingScene.LeaveScene((gc) => { LeaveSceneCompleteAndStartGC(OnGCEndAndEnterScene, gc); });
        }
    }

    /// <summary>
    /// 强制卸载当前场景
    /// </summary>
    public void ForceUnloadCurrentScene()
    {
        if (null == CurrentScene || m_loadedScenes.Count == 0)
        {
            Debug.Assert(false);
            return;
        }

        var unloadScene = GetScene(m_loadedScenes.Pop());
        CurrentScene = null;
        if (m_loadedScenes.Count > 0)
        {
            CurrentScene = GetScene(m_loadedScenes.Pop());
        }

        unloadScene.DestoryResHandlerObj();
        unloadScene.LeaveScene((noGc) => { LeaveSceneCompleteAndStartGC(); }
        );
    }

    #endregion
    
    #region 场景切换流程

    /// <summary>
    /// 场景GC
    /// </summary>
    /// <param name="callBack"></param>
    /// <param name="noGC"></param>
    private void LeaveSceneCompleteAndStartGC(Action callBack = null, bool GC = true)
    {
        if (null == CurrentScene)
        {
            return;
        }

        if (GC)
        {
            // 资源释放
            GameLoop.Instance.StartCoroutine(GameLoop.Instance.Ctx.Get<ResMgr>().OnChangeScene(callBack));
        }
        else
        {
            callBack?.Invoke();
        }
    }

    /// <summary>
    /// GC完成加载新场景
    /// </summary>
    private void OnGCEndAndEnterScene()
    {
        if (null != CurrentScene)
        {
            if (m_loadSceneMode == LoadSceneMode.Single)
            {
                Debug.Assert(m_loadedScenes.Count <= 1);
                m_loadedScenes.Clear();
            }
            else if (m_loadSceneMode == LoadSceneMode.Additive)
            {
                Debug.Assert(m_loadedScenes.Count <= 1);
            }
            else
            {
                Debug.Assert(false);
            }

            m_loadedScenes.Push(CurrentScene.SceneType);
            CurrentScene.EnterScene(EnterSceneComplete, m_loadSceneMode);
        }
    }

    /// <summary>
    /// 新场景加载完成
    /// </summary>
    private void EnterSceneComplete()
    {
        Debug.Log("EnterSceneComplete..........................");

        if (CurrentScene != null)
        {
            CurrentScene.LoadingEnd();
        }

        GameLoop.Instance.Ctx.Get<UIMgr>().Hide(UIEnum.LoadingPanel);
      

        SwitchSceneComplete = true;
        //场景切换完毕，调用几次GC
        GC.Collect(); //GC回收
        GC.Collect(); //GC回
        GC.Collect(); //GC回收
    }

    #endregion

    #region 获取场景信息

    public void SetCurrentSceneVisible(bool visible)
    {
        CurrentScene.rootObj.SetActive(visible);
    }

    public GotSceneBase GetScene(GotSceneType sceneType)
    {
        GotSceneBase scene;
        m_scenes.TryGetValue(sceneType, out scene);
        return scene;
    }

    public T GetScene<T>(GotSceneType sceneType) where T : GotSceneBase
    {
        GotSceneBase scene;
        m_scenes.TryGetValue(sceneType, out scene);
        return scene as T;
    }

    public bool IsInScene(GotSceneType sceneType)
    {
        if (CurrentScene == null)
            return false;

        return (CurrentScene.SceneType == sceneType);
    }

    #endregion

    public void Init(GameContext ctx)
    {
        SceneRoot = new GameObject("SceneRoot");
        DefaultBackgroundLoadingPriority = Application.backgroundLoadingPriority;
        DefaultAsyncUploadBufferSize = QualitySettings.asyncUploadBufferSize;
        DefaultAsyncUploadTimeSlice = QualitySettings.asyncUploadTimeSlice;
        AddScene<GameMainScene>(SceneRoot);
        AddScene<GameStartScene>(SceneRoot);
        m_loadedScenes = new Stack<GotSceneType>();
    }

    public void Shutdown()
    {
       
    }
    
}