using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Properties
{
    public UnityAction OnDead;
    private float _hp;
    private float _atk;
    private float _def;
    private Camp _camp;
    public float HP
    {
        get
        {
            return _hp;
        }
        set
        {
            
            _hp = value;
            if (_hp <= 0)
            {
                OnDead();
            }
        }
    }
    public float ATK
    {
        get
        {
            return _atk;
        }
        set
        {
            _atk = value;
        }
    }
    public float DEF
    {
        get
        {
            return _def;
        }
        set
        {
            _def = value;
        }
    }
    public Camp Camp
    {
        get
        {
            return _camp;
        }
        set
        {
            _camp = value;
        }
    }
}
