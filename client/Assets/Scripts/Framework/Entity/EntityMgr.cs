using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

/// <summary>
/// �������롢����ʵ�塣����ʵ���update�ȷ��������Կ��Ƶ���ʵ���ʱ������
/// </summary>
public class EntityMgr:IGameService,ITickable,IFixedTickable
{
    Dictionary<int,BaseEntity> entities = new Dictionary<int,BaseEntity>();
    List<BaseEntity> baseEntities = new List<BaseEntity>();
    private float UpdateTime=0.1f;
    private float currentUpdateTime=0;
    int i;
    public void  _AddEntity(BaseEntity baseEntity)
    {
        entities[baseEntity._entityID]=baseEntity;
        baseEntities.Add(baseEntity);
    }
    public void _RemoveEntity(BaseEntity baseEntity) {
        if (entities.ContainsKey(baseEntity._entityID))
        {
            entities.Remove(baseEntity._entityID);
            baseEntities.Remove(baseEntity); 
        }
   
    }
    
    public void _FixedUpdate(float deltaTime)
    {
  
    }

    public void Init(GameContext ctx)
    {
        entities.Clear();
        baseEntities.Clear();
    }

    public void Shutdown()
    {
        throw new System.NotImplementedException();
    }

    public void Tick(float dt)
    {
        currentUpdateTime+=dt;
        while (currentUpdateTime>=UpdateTime)
        {
            currentUpdateTime -= UpdateTime;
        }
        for (i = 0; i < baseEntities.Count; i++)
        {
            if (baseEntities[i].IsLoaded)
            {
                baseEntities[i].YOTOUpdate(dt);
            }
         
        }
    }

    public void FixedTick(float fdt)
    {
        for (i = 0; i < baseEntities.Count; i++)
        {
            if (baseEntities[i].IsLoaded)
            {
                baseEntities[i].YOTOFixedUpdate(fdt);
            }
        }
    }
}   
