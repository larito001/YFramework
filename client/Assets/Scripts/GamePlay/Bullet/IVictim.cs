using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IVictim
{
    public Transform GetTransform();
    public int GetId();
    public Properties GetProperties();
    public void OnHurt(IVictim fireRole, float hurt);
    public Vector3 GetPosition();
    public void OnHurtSomeone();
}
