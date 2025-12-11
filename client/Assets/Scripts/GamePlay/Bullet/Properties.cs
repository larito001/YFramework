using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum RoleState
{
    Alive,
    Dead
}
public class Properties
{
    public UnityAction OnDead;
    private float _hp;
    private float _atk;
    private float _def;
    private Camp _camp;
    private RoleState _state;
    public float HP
    {
        get
        {
            return _hp;
        }
        set
        {
            
            _hp = value;
            if (_hp <= 0&&_state==RoleState.Alive)
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
    public RoleState State
    {
        get
        {
            return _state;
        }
        set
        {
            _state = value;
        }
    }
}
