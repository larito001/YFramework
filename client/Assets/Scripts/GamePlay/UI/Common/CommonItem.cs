using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CommonItem : YOTOScrollViewItem
{
  public Button itemButton;
  public TextMeshProUGUI itemCount;
  private int towerId;
  TowerBaseCtrlEntity towerBase;
  public void SetData(Vector2Int item)
  {
    itemCount.text = item.y.ToString();
  }

  public void SetTowerData(int id,TowerBaseCtrlEntity ctrl)
  {
    towerBase = ctrl;
    towerId = id;
    itemCount.text = string.Empty;
    itemButton.onClick.RemoveAllListeners();
    itemButton.onClick.AddListener(OnClickCreateTower);
  }

  private void OnClickCreateTower()
  {
    towerBase.GenerateTowerById(towerId);
  }
}
