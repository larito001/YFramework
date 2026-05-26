using System;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public enum LoadSceneMode
{
    Single = 0,
    Additive = 1,
}

public class YSceneManager : IGameService
{
    public static ThreadPriority LoadingBackgroundLoadingPriority = ThreadPriority.High;
    public static int LoadingAsyncUploadBufferSize = 4;
    public static int LoadingAsyncUploadTimeSize = 33;

    public static ThreadPriority DefaultBackgroundLoadingPriority;
    public static int DefaultAsyncUploadBufferSize;
    public static int DefaultAsyncUploadTimeSlice;

    public GameObject SceneRoot { get; private set; }
    public YSceneBase CurrentScene { get; private set; }
    public YSceneType PreSceneType { get; private set; }
    public bool SwitchSceneComplete { get; private set; }
    public YSceneType SceneType => CurrentScene == null ? YSceneType.None : CurrentScene.SceneType;

    private readonly Dictionary<YSceneType, YSceneBase> scenes = new Dictionary<YSceneType, YSceneBase>();
    private readonly List<Type> registeredSceneTypes = new List<Type>();

    private Stack<YSceneType> loadedScenes;
    private LoadSceneMode currentLoadSceneMode = LoadSceneMode.Single;
    private GameContext context;
    private UIMgr uiMgr;
    private ResMgr resMgr;
    private SceneReferenceService sceneReferenceService;
    private ICoroutineRunner coroutineRunner;

    public void RegisterScene<T>() where T : YSceneBase, new()
    {
        var sceneType = typeof(T);
        if (registeredSceneTypes.Contains(sceneType))
        {
            return;
        }

        registeredSceneTypes.Add(sceneType);
        if (SceneRoot != null)
        {
            AddSceneInstance(new T(), SceneRoot);
        }
    }

    public void SwitchScene(YSceneType sceneType, object args = null, bool showLoading = true,
        LoadSceneMode loadSceneMode = LoadSceneMode.Single)
    {
        Debug.Assert(loadedScenes.Count <= 1);

        var nextScene = GetScene(sceneType);
        if (nextScene == null)
        {
            return;
        }

        if (CurrentScene != null && CurrentScene.SceneType != sceneType)
        {
            PreSceneType = SceneType;
        }

        if (CurrentScene != null && CurrentScene.SceneType == sceneType)
        {
            return;
        }

        SwitchSceneComplete = false;
        currentLoadSceneMode = loadSceneMode;

        var previousScene = CurrentScene;
        CurrentScene = nextScene;
        CurrentScene.SceneArgs = args;

        if (loadSceneMode == LoadSceneMode.Single && previousScene != null)
        {
            previousScene.DestroyResHandlerObj();
        }

        CurrentScene.InitResHandlerObj();

        if (showLoading)
        {
            uiMgr.ShowLoading();
        }

        if (previousScene == null)
        {
            LeaveSceneCompleteAndStartGC(OnGCEndAndEnterScene, false);
            return;
        }

        if (currentLoadSceneMode == LoadSceneMode.Additive)
        {
            LeaveSceneCompleteAndStartGC(OnGCEndAndEnterScene);
            return;
        }

        previousScene.LeaveScene(gc => LeaveSceneCompleteAndStartGC(OnGCEndAndEnterScene, gc));
    }

    public void ForceUnloadCurrentScene()
    {
        if (CurrentScene == null || loadedScenes.Count == 0)
        {
            Debug.Assert(false);
            return;
        }

        var unloadScene = GetScene(loadedScenes.Pop());
        CurrentScene = null;

        if (loadedScenes.Count > 0)
        {
            CurrentScene = GetScene(loadedScenes.Pop());
        }

        unloadScene.DestroyResHandlerObj();
        unloadScene.LeaveScene(_ => LeaveSceneCompleteAndStartGC());
    }

    public void SetCurrentSceneVisible(bool visible)
    {
        if (CurrentScene != null)
        {
            CurrentScene.rootObj.SetActive(visible);
        }
    }

    public YSceneBase GetScene(YSceneType sceneType)
    {
        scenes.TryGetValue(sceneType, out var scene);
        return scene;
    }

    public T GetScene<T>(YSceneType sceneType) where T : YSceneBase
    {
        return GetScene(sceneType) as T;
    }

    public bool IsInScene(YSceneType sceneType)
    {
        return CurrentScene != null && CurrentScene.SceneType == sceneType;
    }

    public void Init(GameContext ctx)
    {
        context = ctx;
        uiMgr = ctx.Get<UIMgr>();
        resMgr = ctx.Get<ResMgr>();
        sceneReferenceService = ctx.Get<SceneReferenceService>();
        coroutineRunner = ctx.Get<ICoroutineRunner>();

        SceneRoot = new GameObject("SceneRoot");
        DefaultBackgroundLoadingPriority = Application.backgroundLoadingPriority;
        DefaultAsyncUploadBufferSize = QualitySettings.asyncUploadBufferSize;
        DefaultAsyncUploadTimeSlice = QualitySettings.asyncUploadTimeSlice;

        for (int i = 0; i < registeredSceneTypes.Count; i++)
        {
            var scene = Activator.CreateInstance(registeredSceneTypes[i]) as YSceneBase;
            if (scene != null)
            {
                AddSceneInstance(scene, SceneRoot);
            }
        }

        loadedScenes = new Stack<YSceneType>();
    }

    public void Shutdown()
    {
        if (CurrentScene != null)
        {
            CurrentScene.DestroyResHandlerObj();
            CurrentScene = null;
        }

        scenes.Clear();
        loadedScenes?.Clear();
        registeredSceneTypes.Clear();

        if (SceneRoot != null)
        {
            UnityEngine.Object.Destroy(SceneRoot);
            SceneRoot = null;
        }

        uiMgr = null;
        resMgr = null;
        sceneReferenceService = null;
        coroutineRunner = null;
    }

    private void AddSceneInstance(YSceneBase scene, GameObject sceneRoot)
    {
        if (scenes.ContainsKey(scene.SceneType))
        {
            Debug.LogWarning($"Scene '{scene.SceneType}' has already been registered.");
            return;
        }

        scenes[scene.SceneType] = scene;
        scene.Initialize(context);

        var sceneObject = new GameObject(scene.SceneName);
        sceneObject.transform.SetParent(sceneRoot.transform, false);
        sceneObject.SetActive(false);

        scene.rootObj = sceneObject;
        scene.rootTrn = sceneObject.transform;
        scene.InitController();
    }

    private void LeaveSceneCompleteAndStartGC(Action callback = null, bool runGC = true)
    {
        if (CurrentScene == null)
        {
            return;
        }

        if (runGC)
        {
            coroutineRunner.Run(resMgr.OnChangeScene(callback));
            return;
        }

        callback?.Invoke();
    }

    private void OnGCEndAndEnterScene()
    {
        if (CurrentScene == null)
        {
            return;
        }

        if (currentLoadSceneMode == LoadSceneMode.Single)
        {
            Debug.Assert(loadedScenes.Count <= 1);
            loadedScenes.Clear();
        }
        else if (currentLoadSceneMode == LoadSceneMode.Additive)
        {
            Debug.Assert(loadedScenes.Count <= 1);
        }
        else
        {
            Debug.Assert(false);
        }

        loadedScenes.Push(CurrentScene.SceneType);
        CurrentScene.EnterScene(EnterSceneComplete, currentLoadSceneMode);
    }

    private void EnterSceneComplete()
    {
        sceneReferenceService.InvalidateCache();
        CurrentScene?.LoadingEnd();
        uiMgr.HideLoading();
        SwitchSceneComplete = true;
        GC.Collect();
    }
}
