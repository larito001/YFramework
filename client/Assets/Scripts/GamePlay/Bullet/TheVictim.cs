using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void OnVictim(TheVictim bullet);

public interface IVictim
{
    public Properties GetProperties();
    public void OnHurt(float hurt);
    public void OnEnter(Collider other);
    public void OnExit(Collider other);
}

public class TheVictim : MonoBehaviour
{
    public IVictim Victim;

    private BoxCollider boxCollider;

    public void Init(Vector3 size, IVictim  victim)
    {
        Victim = victim;
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.size = size;
    }
    public void OnHurt(float  hurt)
    {
        Victim?.OnHurt(hurt);
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