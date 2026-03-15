using UnityEngine;
using UnityEngine.UI;

public class TowerBaseHud : HudAlwaysFaceToTransform
{
    private static UIMgr sharedUiMgr;

    public static void Configure(UIMgr uiMgr)
    {
        sharedUiMgr = uiMgr;
    }

    public Button up;
    public Button fix;
    public Button remove;
    public Button onClose;

    private Canvas canvas;
    private TowerEntity tower;
    private bool isInit;
    private int level = 1;

    public void Init(TowerEntity towerEntity)
    {
        tower = towerEntity;

        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            canvas.worldCamera = HudAlwaysFaceToTransform.camera;
        }

        gameObject.SetActive(false);
        up.onClick.RemoveAllListeners();
        fix.onClick.RemoveAllListeners();
        remove.onClick.RemoveAllListeners();
        onClose.onClick.RemoveAllListeners();
        up.onClick.AddListener(OnClickUp);
        fix.onClick.AddListener(OnClickFix);
        remove.onClick.AddListener(OnClickRemove);
        onClose.onClick.AddListener(OnHide);
    }

    public void OnShow(int currentLevel)
    {
        level = currentLevel;
        isInit = true;
        ForceLookAt();
        gameObject.SetActive(true);
    }

    public void OnHide()
    {
        isInit = false;
        gameObject.SetActive(false);
    }

    private void OnClickRemove()
    {
        tower.RemoveOnBase();
    }

    private void OnClickFix()
    {
        TowerUpParam param = new TowerUpParam();
        param.confirmAction = OnFixConfirm;
        sharedUiMgr.Show(UIEnum.TowerUpPanel, param);
    }

    private void OnClickUp()
    {
        if (level >= 3)
        {
            return;
        }

        TowerUpParam param = new TowerUpParam();
        param.confirmAction = OnUpConfirm;
        sharedUiMgr.Show(UIEnum.TowerUpPanel, param);
    }

    private void OnFixConfirm()
    {
        if (this != null && isInit && tower.ObjTrans != null)
        {
            tower.OnFix();
        }

        OnHide();
    }

    private void OnUpConfirm()
    {
        if (isInit && tower.ObjTrans != null)
        {
            tower.OnLevelUp();
        }

        OnHide();
    }
}
