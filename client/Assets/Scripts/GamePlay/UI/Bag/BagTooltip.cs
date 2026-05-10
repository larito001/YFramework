using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

public class BagTooltip : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public RectTransform tooltipRoot;
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    private ResMgr resMgr;
    private ResourceHandle<Sprite> iconHandle;

    public void Init(ResMgr res)
    {
        resMgr = res;
        Hide();
    }

    public void ShowFor(Item def, Vector3 anchorPos)
    {
        if (def == null)
        {
            Hide();
            return;
        }
        if (nameText != null) nameText.text = def.Name ?? string.Empty;
        if (descText != null) descText.text = def.Desc ?? string.Empty;
        LoadIcon(def.IconPath);
        if (tooltipRoot != null) tooltipRoot.position = anchorPos;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        ReleaseIcon();
    }

    public void ReleaseIcon()
    {
        if (iconHandle != null)
        {
            iconHandle.Dispose();
            iconHandle = null;
        }
        if (iconImage != null) iconImage.sprite = null;
    }

    private void LoadIcon(string path)
    {
        ReleaseIcon();
        if (iconImage == null || string.IsNullOrEmpty(path) || resMgr == null) return;
        resMgr.LoadHandleAsync<Sprite>(path, handle =>
        {
            if (this == null)
            {
                handle?.Dispose();
                return;
            }
            iconHandle = handle;
            if (handle != null && handle.Asset != null && iconImage != null)
            {
                iconImage.sprite = handle.Asset;
                iconImage.enabled = true;
            }
        });
    }

    private void OnDestroy()
    {
        ReleaseIcon();
    }
}
