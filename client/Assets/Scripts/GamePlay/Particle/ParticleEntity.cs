using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public struct ParticleEntityData
{
    public string path;
    public Vector3 pos;
    public float scale;
}

public class ParticleEntity : ObjectBase, PoolItem<ParticleEntityData>
{
    public static DataObjPool<ParticleEntity, ParticleEntityData> pool =
        new DataObjPool<ParticleEntity, ParticleEntityData>("ParticleEntity", 4);

    private ParticleEntityData _data;
    private bool needPlay = false;
    private bool loaded = false;

    public void AfterIntoObjectPool()
    {
        loaded = false;
        needPlay = false;
        SetInVision(false);
        RecoverObject();
    }

    public void SetData(ParticleEntityData data)
    {
        Location = data.pos;
        _data = data;
        loaded = false;
        SetInVision(true);
        SetPrefabBundlePath(data.path);
        InstanceGObj();
    }
    
    private void PlayParticle()
    {
        //获取obj及其子节点的所有粒子，然后播放
        var list = ObjTrans.GetComponentsInChildren<ParticleSystem>();
        foreach (var item in list)
        {
            item.Play();
        }
        Timers.inst.Add(1,DelayRemove);
    }

    private void DelayRemove(object obj)
    {
        pool.RecoverItem(this);
    }

    public void Play()
    {
        if (loaded)
        {
            PlayParticle();
        }
        else
        {
            needPlay = true;
        }
    }

    public override string GetModelLayer()
    {
        return "Default";
    }

    protected override void AfterInstanceGObj()
    {
        loaded = true;
        if (needPlay)
        {
            Play();    
        }
        ObjTrans.localScale= new Vector3(_data.scale, _data.scale, _data.scale);

    }

    protected override void BeforeRecover(bool isDelete)
    {
    }
}