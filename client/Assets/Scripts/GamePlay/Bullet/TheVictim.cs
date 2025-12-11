using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void OnVictim(TheVictim bullet);

public interface IVictim
{
    public int GetId();
    public Properties GetProperties();
    public void OnHurt(IVictim fireRole,float hurt);
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

    public void Init(Vector3 size, IVictim  victim)
    {
        Victim = victim;
        ID = Victim.GetId();
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = size;
    }
    public void OnHurt(IVictim fireRole,float  hurt)
    {
        Victim?.OnHurt(fireRole,hurt);
    }
    //索敌
    private void OnTriggerEnter(Collider other)
    {
        Victim?.OnEnter( other);
    }

    private void OnTriggerStay(Collider other)
    {
    }

    private void OnTriggerExit(Collider other)
    {
        Victim?.OnExit(other);
    }
}