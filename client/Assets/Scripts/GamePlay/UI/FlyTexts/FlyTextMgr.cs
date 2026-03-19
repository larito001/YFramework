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

public class FlyTextMgr : IGameService, ITickable
{
    private readonly Queue<FlyTextData> flyTextQueue = new Queue<FlyTextData>();
    private int generateNumPerFrame = 200;
    private Camera cam;
    private IUIService uiMgr;

    public void Init(int perFrame = 1)
    {
        generateNumPerFrame = perFrame;
    }

    public void AddText(string text, Vector3 pos, FlyTextType type = FlyTextType.Normal, TextPosType posType = TextPosType.World)
    {
        FlyTextData data = new FlyTextData();
        data.text = text;

        Vector3 screenPosition = posType == TextPosType.World ? cam.WorldToScreenPoint(pos) : pos;
        screenPosition.z = 0;
        data.pos = screenPosition;
        data.flyTextType = type;
        flyTextQueue.Enqueue(data);
    }

    public void AddTextAtScreenCenter(string text, FlyTextType type = FlyTextType.Normal)
    {
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        AddText(text, screenCenter, type, TextPosType.Screen);
    }

    public void Init(GameContext ctx)
    {
        cam = ctx.Get<CameraMgr>().getMainCamera();
        uiMgr = ctx.Get<UIMgr>();
    }

    public void Shutdown()
    {
    }

    public void Tick(float dt)
    {
        for (int index = 0; flyTextQueue.Count > 0 && index < generateNumPerFrame; index++)
        {
            var layerRoot = uiMgr?.GetLayerRoot(UILayerEnum.PopText);
            if (layerRoot == null)
            {
                return;
            }

            FlyTextCtrl textBatcher = FlyTextCtrl.pool.GetItem(layerRoot);
            textBatcher.InstanceGObj();
            textBatcher.Fly(flyTextQueue.Dequeue());
        }
    }
}
