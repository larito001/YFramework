using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CommonItem : YOTOScrollViewItem
{
    public Button itemButton;
    public TextMeshProUGUI itemCount;
    public TextMeshProUGUI itemName;
    public Image itemIcon;
    private int towerId;
    TowerBaseCtrlEntity towerBase;

    public void SetData(Vector2Int item)
    {
        itemCount.text = item.y.ToString();
        // var itemdata = BagPlugin.Instance.GetItemData(item.x);
        // itemName.text = itemdata.Name;
        // itemIcon.sprite = itemdata.Icon;
    }

    // public void SetTowerData(TowerData data, TowerBaseCtrlEntity ctrl)
    // {
    //     towerBase = ctrl;
    //     towerId = data.Id;
    //     itemCount.text = string.Empty;
    //     itemButton.onClick.RemoveAllListeners();
    //     itemButton.onClick.AddListener(OnClickCreateTower);
    //     itemName.text = data.Name;
    // }
    //
    // private void OnClickCreateTower()
    // {
    //     towerBase.GenerateTowerById(towerId);
    // }
}