using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class SelectTowerItem : YOTOScrollViewItem
{
    public Button btn;
    public YOTOScrollView scrollView;
    public  Image icon;
    public TextMeshProUGUI name;
    public TextMeshProUGUI des;
    TowerData _towerData;
    public void RefreshItem(TowerData towerData)
    {
        _towerData = towerData;
        scrollView.Initialize();
        scrollView.SetData(towerData.UseIdAndNumber.Count);
        scrollView.SetRenderer(ItemRender);
        name.text = towerData.Name;
        icon.sprite = towerData.Icon;
        btn.onClick.RemoveAllListeners();
        des.text = towerData.des;
        
        bool canBuild = true;
        foreach (var vector2Int in towerData.UseIdAndNumber)
        {
            var haveNum = BagPlugin.Instance.GetItemNum(vector2Int.x);
            if (haveNum < vector2Int.y)
            {
                canBuild = false;
            }
        }

        if (canBuild)
        {
            btn.onClick.AddListener(OnSelectTower);
            btn.GetComponent<Image>().color = Color.white;
        }
        else
        {
            btn.GetComponent<Image>().color = Color.gray;
        }
        
    }

    private void OnSelectTower()
    {
        
        TowerManager.Instance.ClickGennerateTower(_towerData.Id);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int index)
    {
        var item = arg1 as CommonItem;
        item.SetData(new Vector2Int(_towerData.UseIdAndNumber[index].x,_towerData.UseIdAndNumber[index].y));
    }
}