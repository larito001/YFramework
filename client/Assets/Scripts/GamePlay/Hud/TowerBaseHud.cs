using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TowerBaseHud : HudAlwaysFaceToTransform
{
   public YOTOScrollView scrollView;
   Canvas canvas;
   TowerBaseCtrlEntity towerBaseCtrl;
   private bool isInit = false;

   public void Init(TowerBaseCtrlEntity ctrl)
   {
      towerBaseCtrl = ctrl;
      scrollView.Initialize(10);
      scrollView.SetRenderer(ItemRender);



      if (canvas == null)
      {
         canvas = GetComponent<Canvas>();
         canvas.worldCamera = YFramework.cameraMgr.getMainCamera();
      }

      gameObject.SetActive(false);
   }
   public void OnShow()
   {
      gameObject.SetActive(true);
      scrollView.SetData(TowerManager.Instance.towerDatas.Count);
   }

   private void ItemRender(YOTOScrollViewItem obj, int index)
   {
      var item = obj as CommonItem;
      item.SetTowerData(TowerManager.Instance.towerDatas[index], towerBaseCtrl);
   }

   public void OnHide()
   {
      scrollView.SetData(0);
      gameObject.SetActive(false);
   }
}
