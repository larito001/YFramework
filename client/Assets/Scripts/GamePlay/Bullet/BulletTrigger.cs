using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletTrigger : MonoBehaviour
{
    private BaseBulletEntity bullet;
    private bool isInit = false;
    public void Init(BaseBulletEntity bullet)
    {
        this.bullet = bullet;
        isInit = true;
    }

    public void Unload()
    {
        isInit = false;
    }
    private void OnTriggerEnter(Collider other)
    {
        if(isInit)
        bullet.TriggerEnter();
    }

    private void OnTriggerStay(Collider other)
    {
        if(isInit)
        bullet.TriggerStay();
    }

    private void OnTriggerExit(Collider other)
    {
        if(isInit)
        bullet.TriggerExit();
    }
}