using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void OnVictim(TheVictim bullet);

public interface IVictim
{
    public Transform GetTransform();
    public int GetId();
    public Properties GetProperties();
    public void OnHurt(IVictim fireRole, float hurt);
    public void OnEnter(Collider other);
    public void OnExit(Collider other);
    public Vector3 GetPosition();
    public void OnHurtSomeone();
}

public class TheVictim : MonoBehaviour
{
    public IVictim Victim;
    public int ID = 0;
    private BoxCollider boxCollider;
    private AtkRangeCtrl atkRangeCtrl;

    public void Init(Vector3 boxSize,Vector3 rangeSize, IVictim victim)
    {
        Victim = victim;
        ID = Victim.GetId();
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = boxSize;
        GameObject obj = new GameObject("AtkRange");
        atkRangeCtrl= obj.AddComponent<AtkRangeCtrl>();
        atkRangeCtrl.Init(rangeSize, victim);

    
 
    }

    public void Remove()
    {
        Victim = null;
        atkRangeCtrl.Remove();

        atkRangeCtrl = null;
        GameObject.Destroy(this);
    }
    public void OnHurt(IVictim fireRole, float hurt)
    {
        Victim?.OnHurt(fireRole, hurt);
    }
    
}