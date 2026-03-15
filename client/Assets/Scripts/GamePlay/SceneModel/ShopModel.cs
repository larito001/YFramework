using UnityEngine;
using UnityEngine.UI;

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
        UIManager.Show(UIEnum.ShopPanel);
    }
}
