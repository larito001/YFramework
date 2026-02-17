using UnityEngine;

[CreateAssetMenu(menuName = "GamePlay/Build/TowerConfig")]
public class TowerConfigSO : ScriptableObject
{
    public int towerId;
    public string displayName;

    [Header("Prefab")]
    public GameObject prefab;
    public GameObject previewPrefabOverride; // 可空：预览用更轻模型

    [Header("Build Rules")]
    public Vector3 footprintExtents = new Vector3(0.5f, 1.0f, 0.5f); // OverlapBox 半尺寸
    public float maxSlopeAngle = 25f; // 地面法线与Up夹角上限
    public float snapStep = 0f;       // 0=不吸附；>0=按格吸附（世界坐标）

    [Header("Cost")]
    public string costCurrency = "Gold";
    public int buildCost = 10;
    public int recycleRefund = 5;

    [Header("Upgrade")]
    public TowerConfigSO upgradeTo;   // 下一等级塔（可空）
    public int upgradeCost = 15;
}