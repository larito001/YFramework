using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CommonItem : YOTOScrollViewItem
{
  public TextMeshProUGUI itemCount;
  public void SetData(Vector2Int item)
  {
    itemCount.text = item.y.ToString();
  }
}
