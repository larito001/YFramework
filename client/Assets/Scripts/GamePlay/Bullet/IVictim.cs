using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IVictim
{
    public Properties GetProperties();
    public void OnHurt(IVictim fireRole, float hurt);
    public Vector3 GetPosition();
    public List<Vector3> GetAtkSlot();
    public Vector3 GetForward();
    public void OnHurtSomeone();
    public void OnSlowDown(float rate);
}
