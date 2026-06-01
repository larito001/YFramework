using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>一条结算明细:动物名 + 击杀数量 + 该种累计积分。</summary>
public class HuntResultEntry
{
    public string animalName;
    public int count;
    public int score;
}

/// <summary>一局打猎的结算数据:逐种击杀明细 + 总积分。由 <see cref="GameMainPanel"/> 结束打猎时组装并传入。</summary>
public class HuntResult
{
    public int totalScore;
    public List<HuntResultEntry> entries = new List<HuntResultEntry>();
}

/// <summary>
/// 打猎结算界面(<see cref="UIEnum.FinishPanel"/>):结束打猎确认后显示。
///   顶部标题 + 逐种击杀明细列表(动物 ×数量 +积分) + 总积分 + 获得金币 + 「返回大厅」。
/// 数据由 <see cref="GameMainPanel"/> 通过 <see cref="HuntResult"/> 传入(不自己读配表)。
/// **结算发奖**:打中猎物的积分按 1:1 折算成金币,在本界面显示时一次性入账(<see cref="CurrencySystem"/>)并立即写盘——
/// 这是「打死猎物结算后给金币」的唯一入账点。确定后清掉场上动物并回到大厅。
/// 预制体由 <c>Tools/UI/Build FinishPanel Prefab</c> 生成。
/// </summary>
public class FinishPanel : UIPageBase<HuntResult>
{
    [Header("文本")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI totalText;
    public TextMeshProUGUI rewardText; // 获得金币(可选;未挂则并入 totalText 显示)

    [Header("明细列表(ScrollRect 的 VerticalLayoutGroup 容器)")]
    public RectTransform content;

    [Header("按钮")]
    public Button confirmBtn;

    private static readonly Color RowBg = new Color(0.90f, 0.89f, 0.93f, 1f);
    private static readonly Color NameText = new Color(0.25f, 0.24f, 0.30f, 1f);
    private static readonly Color ScoreText = new Color(0.85f, 0.30f, 0.25f, 1f);

    private TMP_FontAsset font;
    private CurrencySystem currency;
    private int grantedGold; // 本局结算入账的金币,OnShow 时弹通用领取弹窗展示

    public override void OnLoad()
    {
        if (titleText != null) font = titleText.font;
        currency = GetService<CurrencySystem>();
        if (confirmBtn != null) confirmBtn.onClick.AddListener(OnConfirm);
    }

    protected override void OnBeforeShow(HuntResult result)
    {
        if (titleText != null) titleText.text = "打猎结算";
        BuildList(result);

        int score = result != null ? result.totalScore : 0;
        if (totalText != null) totalText.text = $"总积分：{score}";

        // 结算发金币:积分 1:1 折算金币,本局打中才有(没命中=0,不发也不写盘)。
        // 入账后立即写盘,保证返回大厅/下次进图读到的是发奖后的余额。
        int gold = score;
        if (gold > 0 && currency != null)
        {
            currency.Add(CurrencyType.Gold, gold);
            currency.Save();
        }
        grantedGold = gold;

        string rewardLine = $"获得金币：+{gold}";
        if (rewardText != null) rewardText.text = rewardLine;
        else if (totalText != null) totalText.text += $"\n{rewardLine}"; // 无独立奖励文本则并入总分行
    }

    public override void OnShow()
    {
        // 通用奖励领取弹窗(Top 层,1 秒自动消失):金币已入账,这里仅展示。在 OnShow 弹,避免在 OnBeforeShow 阶段叠开界面。
        if (grantedGold > 0)
            Show<RewardClaimPanel, RewardClaimParam>(new RewardClaimParam
            {
                title = "打猎结算",
                rewards = { RewardEntry.Currency(CurrencyType.Gold, grantedGold) },
            });
    }
    public override void OnHide() { }
    public override void OnResize() { }

    private void BuildList(HuntResult result)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        if (result == null || result.entries.Count == 0)
        {
            BuildEmptyRow();
            return;
        }
        foreach (var e in result.entries) BuildRow(e);
    }

    private void BuildRow(HuntResultEntry e)
    {
        var row = NewChild(content, $"Row_{e.animalName}", out _);
        var bg = row.AddComponent<Image>();
        bg.color = RowBg;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 110;

        // 动物名(左)
        var name = NewChild(row.transform, "Name", out var nameRt);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(0.55f, 1); nameRt.offsetMin = new Vector2(30, 0); nameRt.offsetMax = Vector2.zero;
        NewText(name, e.animalName, 42, NameText, TextAlignmentOptions.Left);

        // ×数量(中)
        var cnt = NewChild(row.transform, "Count", out var cntRt);
        cntRt.anchorMin = new Vector2(0.55f, 0); cntRt.anchorMax = new Vector2(0.78f, 1); cntRt.offsetMin = cntRt.offsetMax = Vector2.zero;
        NewText(cnt, $"×{e.count}", 42, NameText, TextAlignmentOptions.Center);

        // +积分(右)
        var sc = NewChild(row.transform, "Score", out var scRt);
        scRt.anchorMin = new Vector2(0.78f, 0); scRt.anchorMax = new Vector2(1, 1); scRt.offsetMin = Vector2.zero; scRt.offsetMax = new Vector2(-30, 0);
        NewText(sc, $"+{e.score}", 42, ScoreText, TextAlignmentOptions.Right);
    }

    private void BuildEmptyRow()
    {
        var row = NewChild(content, "Empty", out _);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 110;
        NewText(row, "本局未命中任何动物", 42, NameText, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// 返回大厅:切到大厅场景(GamePlay)。离开对局(Home)会清掉场上动物,
    /// 大厅场景 OnLoadingEnd 负责收起 HUD/结算并显示主界面——不在这里手动开关 UI,
    /// 以保证「再次出发」是一次真正的 Home 场景切换(同场景切换会被忽略)。
    /// </summary>
    private void OnConfirm()
    {
        CloseSelf();
        GetService<YSceneManager>().SwitchScene(YSceneType.GamePlay);
    }

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private void NewText(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = color;
        tmp.raycastTarget = false; tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
