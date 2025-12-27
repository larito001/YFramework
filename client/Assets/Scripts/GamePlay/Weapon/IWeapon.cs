using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IWeapon
{
    public void OnUse();
    public void OnShoot(IVictim fireRole,Vector3 hitPoint,float dt);
    public void OnUnUse();
    public void OnDie();
}