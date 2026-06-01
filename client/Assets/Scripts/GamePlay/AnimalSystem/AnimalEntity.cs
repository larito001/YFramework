using UnityEngine;

/// <summary>
/// 场景中的动物实例(低多边形动物模型,目前只播 idle 动画)。持有配表 id 与击杀积分,
/// 供后续「射击命中判定」从射线命中物上取用(命中即加分/销毁)。
/// </summary>
public class AnimalEntity : MonoBehaviour
{
    public int animalId;
    public int score;
}
