using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void OnVictim(TheVictim bullet);

public interface IVictim
{
    public Properties GetProperties();
    public void OnHurt(float hurt);
}

public class TheVictim : MonoBehaviour
{
    public IVictim Victim;
}