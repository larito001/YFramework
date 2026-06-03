using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 设置界面:竖屏单页,保留声音设置(按键 / 画面页签已移除) + 两个调试按钮(清空数据 / 加金币)。
/// 声音控件由 <see cref="SoundSettingsTab"/> 在激活时懒构建并绑定 SoundMgr;本类负责显示/返回与调试按钮。
/// 预制体由 <c>Tools/UI/Build SettingPanel Prefab</c> 生成。
/// </summary>
public class SettingPanel : UIPageBase
{
    public Button backBtn;
    public Button logoutBtn; // 退出登录
    public GameObject soundTab;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private StoreMgr store;       // 仅调试按钮使用
    private CurrencySystem currency;
    private EventMgr eventMgr;
#endif

    public override void OnLoad()
    {
        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (logoutBtn != null) logoutBtn.onClick.AddListener(OnLogoutClick);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        store = GetService<StoreMgr>();
        currency = GetService<CurrencySystem>();
        eventMgr = GetService<EventMgr>();

        // 调试按钮(左下角竖排):清空数据并刷新 / 金币 +10000。仅编辑器/开发包,正式包不出现,防止玩家清档/刷金币。
        CreateDebugButton("清空数据并刷新", new Vector2(40f, 150f), new Color(0.78f, 0.30f, 0.30f, 1f), OnClearData);
        CreateDebugButton("金币 +10000", new Vector2(40f, 44f), new Color(0.85f, 0.66f, 0.20f, 1f), OnAddGold);
#endif
    }

    public override void OnShow()
    {
        if (soundTab != null) soundTab.SetActive(true);
        // 设置页底部横幅广告(原生浮层)。未接入广告 / 非 Android 时 IAdService 取不到或为空操作,自动跳过。
        if (Context.TryGet<IAdService>(out var ad)) ad.ShowBottomBanner("settings");
    }

    public override void OnHide()
    {
        // 关闭设置页即收掉横幅,释放原生资源(避免横幅残留在其它界面)。
        if (Context.TryGet<IAdService>(out var ad)) ad.HideBanner();
    }

    public override void OnResize()
    {
    }

    // ---------------- 退出登录 ----------------

    /// <summary>点「退出登录」:先弹确认弹窗,确认后才真正登出(防误触)。</summary>
    private void OnLogoutClick()
    {
        Show<ConfirmPanel, ConfirmParam>(new ConfirmParam
        {
            title = "退出登录",
            message = "确定退出当前 TapTap 账号?",
            confirmText = "退出",
            cancelText = "取消",
            onConfirm = DoLogout,
        });
    }

    /// <summary>登出当前账号并回到登录界面:关大厅与本设置页,显示登录门等待重新登录。</summary>
    private void DoLogout()
    {
        if (Context.TryGet<ILoginService>(out var login)) login.Logout();
        Show<LoginPanel>(); // 显示登录门
        Hide<StartPanel>(); // 收起大厅
        CloseSelf();        // 关设置页
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ---------------- 调试按钮(仅编辑器/开发包) ----------------

    /// <summary>清空当前存档槽的进度数据(装备/货币/背包/图鉴/任务)并重载,触发各界面刷新。</summary>
    private void OnClearData()
    {
        if (store == null) return;
        store.ClearSaves(SaveCategory.Progress);      // 删当前槽的进度落盘文件
        store.LoadAll(() =>                            // 重载:读到空档 → 各系统 Restore 回默认(装备重新发种子,货币清零等)
        {
            eventMgr?.Trigger(YOTOEventType.RefreshCurrency);
            eventMgr?.Trigger(YOTOEventType.RefreshLoadout);
            eventMgr?.Trigger(YOTOEventType.RefreshCodex);
            eventMgr?.Trigger(YOTOEventType.RefreshBagList);
            eventMgr?.Trigger(YOTOEventType.RefreshTask);
            GetService<FlyTextMgr>()?.AddTextAtScreenCenter("数据已清空");
        });
    }

    /// <summary>金币 +10000(立即落盘,变化即触发 RefreshCurrency 刷新各处金币显示)。</summary>
    private void OnAddGold()
    {
        if (currency == null) return;
        currency.Add(CurrencyType.Gold, 10000);
        currency.Save();
        GetService<FlyTextMgr>()?.AddTextAtScreenCenter("金币 +10000");
    }

    // ---------------- 工具 ----------------

    /// <summary>运行时建一个左下角锚定的按钮。位置不合适改 anchoredPos。</summary>
    private void CreateDebugButton(string label, Vector2 anchoredPos, Color color, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f); // 左下角
        rt.sizeDelta = new Vector2(380f, 96f);
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color = color;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var labelGo = new GameObject("Label", typeof(RectTransform));
        var lrt = (RectTransform)labelGo.transform;
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = UITheme.Font(32);
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var f = GetFont();
        if (f != null) tmp.font = f;
    }

    /// <summary>复用返回按钮上的中文字体(取不到则用 TMP 默认)。</summary>
    private TMP_FontAsset GetFont()
    {
        var t = backBtn != null ? backBtn.GetComponentInChildren<TextMeshProUGUI>() : null;
        return t != null ? t.font : TMP_Settings.defaultFontAsset;
    }
#endif
}
