using UnityEngine;
using UnityEngine.UI;

public class SkillTreeModel : SceneModelBase
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
        UIManager.Show(UIEnum.SkillTreePanel);
    }
}
