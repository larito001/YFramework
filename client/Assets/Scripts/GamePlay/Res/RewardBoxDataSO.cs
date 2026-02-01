using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "New RewardBoxDataSO", menuName = "RewardBoxDataSO")]
public class RewardBoxDataSO : ScriptableObject
{
    [SerializeField]
    public List<RewardBoxData> rewardDatas = new List<RewardBoxData>();
}
