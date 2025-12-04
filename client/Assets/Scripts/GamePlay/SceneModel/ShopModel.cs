using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class ShopModel : SceneModelBase
{
  public GameObject hud;
  public Button hudBtn;

  private void Start()
  {
    hudBtn.onClick.AddListener(OnClickShop);
  }

  private void OnDestroy()
  {
    hudBtn.onClick.RemoveAllListeners();
  }

  private void OnClickShop()
  {
    YFramework.uIMgr.Show(UIEnum.ShopPanel);
  }

  protected override void OnEnter(Collider other)
  {
    base.OnEnter(other);
    if (other.TryGetComponent(out ThirdPlayerMoveCtrl player))
    {
      hud.SetActive(true); 
    }

  }
  protected override void OnExit(Collider other)
  {
    base.OnExit(other);
    if (other.TryGetComponent(out ThirdPlayerMoveCtrl player))
    {
      hud.SetActive(false); 
      YFramework.uIMgr.Hide(UIEnum.ShopPanel);
    }

  }
}
