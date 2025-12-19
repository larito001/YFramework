using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New TowerData", menuName = "TowerData")]
public class TowerDataSO : ScriptableObject
{
    [SerializeField] public List<TowerData> TowerDatas = new();
}