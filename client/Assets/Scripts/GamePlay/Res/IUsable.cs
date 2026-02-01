using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IUser
{
    public void OnUsing();
    public void OnStopUsing();
}

public interface IUsable
{
    public void OnUse(IUser  user);
    public Vector3 GetPosition();
    public void UnUse(IUser  user);
}

