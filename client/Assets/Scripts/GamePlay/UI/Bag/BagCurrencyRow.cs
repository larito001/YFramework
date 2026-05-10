using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

public class BagCurrencyRow : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI countText;

    private ResMgr resMgr;
    private ResourceHandle<Sprite> iconHandle;
    private int currentItemId;

    public void Init(ResMgr res)
    {
        resMgr = res;
        Clear();
    }

    public void Setup(Item def, long count)
    {
        if (def == null)
        {
            Clear();
            return;
        }
        if (countText != null) countText.text = count.ToString();
        if (currentItemId != (int)def.Id)
        {
            currentItemId = (int)def.Id;
            LoadIcon(def.IconPath);
        }
        gameObject.SetActive(true);
    }

    public void Clear()
    {
        currentItemId = 0;
        if (countText != null) countText.text = "0";
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        ReleaseIcon();
        gameObject.SetActive(false);
    }

    public void ReleaseIcon()
    {
        if (iconHandle != null)
        {
            iconHandle.Dispose();
            iconHandle = null;
        }
    }

    private void LoadIcon(string path)
    {
        ReleaseIcon();
        if (iconImage == null || string.IsNullOrEmpty(path) || resMgr == null)
        {
            if (iconImage != null) iconImage.enabled = false;
            return;
        }
        int requested = currentItemId;
        resMgr.LoadHandleAsync<Sprite>(path, handle =>
        {
            if (this == null)
            {
                handle?.Dispose();
                return;
            }
            if (currentItemId != requested)
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
