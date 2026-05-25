using System.Collections.Generic;
using UnityEngine;
using YOTO;

public enum FlyTextType
{
    Normal = 0,
    Quick = 1,
    PlayerHurt = 2,
    AddHP = 3,
}

public enum TextPosType
{
    World,
    Screen
}

public struct FlyTextData
{
    public string text;
    public Vector3 pos;
    public FlyTextType flyTextType;
}

public class FlyTextMgr : MonoBehaviour
{
    private const string PrefabPath = "UI/FlyText/FlyTextPrefab";

    private readonly Queue<FlyTextData> flyTextQueue = new Queue<FlyTextData>();
    private readonly Stack<FlyTextCtrl> pool = new Stack<FlyTextCtrl>();
    private int generateNumPerFrame = 200;

    private Camera cam;
    private IUIService uiMgr;
    private ResMgr resMgr;

    private GameObject prefab;
    private ResourceHandle<GameObject> prefabHandle;
    private bool prefabLoading;

    public void AddText(string text, Vector3 pos, FlyTextType type = FlyTextType.Normal, TextPosType posType = TextPosType.World)
    {
        var data = new FlyTextData
        {
            text = text,
            flyTextType = type,
        };
        Vector3 screenPosition = posType == TextPosType.World ? cam.WorldToScreenPoint(pos) : pos;
        screenPosition.z = 0;
        data.pos = screenPosition;
        flyTextQueue.Enqueue(data);
    }

    public void AddTextAtScreenCenter(string text, FlyTextType type = FlyTextType.Normal)
    {
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        AddText(text, screenCenter, type, TextPosType.Screen);
    }

    private void Awake()
    {
        var ctx = GameLoop.Instance.Ctx;
        cam = ctx.Get<CameraMgr>().MainCamera;
        uiMgr = ctx.Get<UIMgr>();
        resMgr = ctx.Get<ResMgr>();
    }

    private void OnDestroy()
    {
        flyTextQueue.Clear();
        pool.Clear();
        prefabHandle?.Release();
        prefabHandle = null;
        prefab = null;
    }

    private void Update()
    {
        if (prefab == null)
        {
            EnsurePrefabLoading();
            return;
        }

        for (int i = 0; flyTextQueue.Count > 0 && i < generateNumPerFrame; i++)
        {
            var layerRoot = uiMgr?.GetLayerRoot(UILayerEnum.PopText);
            if (layerRoot == null) return;

            var ctrl = Acquire(layerRoot);
            ctrl.Fly(flyTextQueue.Dequeue());
        }
    }

    private FlyTextCtrl Acquire(Transform parent)
    {
        FlyTextCtrl ctrl;
        if (pool.Count > 0)
        {
            ctrl = pool.Pop();
            ctrl.transform.SetParent(parent, false);
            ctrl.gameObject.SetActive(true);
        }
        else
        {
            var go = Object.Instantiate(prefab, parent, false);
            ctrl = go.GetComponent<FlyTextCtrl>();
            ctrl.Bind(Recycle);
        }
        return ctrl;
    }

    private void Recycle(FlyTextCtrl ctrl)
    {
        if (ctrl == null) return;
        ctrl.gameObject.SetActive(false);
        ctrl.transform.SetParent(null, false);
        pool.Push(ctrl);
    }

    private void EnsurePrefabLoading()
    {
        if (prefabLoading || resMgr == null) return;
        prefabLoading = true;
        resMgr.LoadHandleAsync<GameObject>(PrefabPath, handle =>
        {
            prefabLoading = false;
            prefabHandle = handle;
            prefab = handle?.Asset;
        });
    }
}
