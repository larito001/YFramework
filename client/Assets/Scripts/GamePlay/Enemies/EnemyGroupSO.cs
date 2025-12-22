using System.Collections;
using System.Collections.Generic;
using UnityEngine;


using UnityEngine;

[CreateAssetMenu(fileName = "New EnemyGroupSO", menuName = "EnemyGroupSO")]
public class EnemyGroupSO : ScriptableObject
{
    [SerializeField] public List<EnemyGroupData> EnemyGroupDatas = new List<EnemyGroupData>();
}