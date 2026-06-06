#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 按钮工厂:集中「实例化通用按钮预制体 + (可选)复位被 art-kit 烤成 3 的根缩放 + 写文字/图标」这段
/// 原本在各 PanelBuilder 里复制粘贴的逻辑(Login/Leaderboard/Finish/Equip/Start 各有一份)。
/// 新面板加按钮、或想统一某个手搓按钮,都只调这里一处即可。
///
/// 下方常量是「可复用按钮预制体」的唯一登记处;想改某类按钮的视觉风格,直接改对应预制体,无需动 Builder。
///   <see cref="CommonButtonPath"/>       绿色主操作按钮
///   <see cref="CommonButtonYellowPath"/> 黄色侧栏功能按钮(可挂图标)
///   <see cref="BackButtonPath"/>         左上返回按钮
///   <see cref="NextButtonPath"/>         分页方向键
///   <see cref="TabPrefabPath"/>          页签
/// backBtn / nextBtn / Tab_01 各面板的后置配置差异较大,仍由各 Builder 自行 InstantiatePrefab;
/// 此处常量供其引用与备查,保证「就这几类按钮」一目了然。
/// </summary>
public static class UIButtonFactory
{
    public const string CommonButtonPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    public const string CommonButtonYellowPath = "Assets/Resources/UI/Common/CommonButtonYellow.prefab";
    public const string BackButtonPath = "Assets/Resources/UI/backBtn.prefab";
    public const string NextButtonPath = "Assets/Resources/UI/nextBtn.prefab";
    public const string TabPrefabPath = "Assets/Resources/UI/Tab_01.prefab";

    /// <summary>
    /// 实例化通用按钮预制体并完成统一处理:命名、激活、(可选)复位根缩放、(可选)写文字与字号。
    /// </summary>
    /// <param name="prefab">已加载的按钮预制体(由调用方 LoadAssetAtPath,沿用其缺失校验与报错)。</param>
    /// <param name="name">实例物体名。</param>
    /// <param name="label">按钮文字;为 null 时保留预制体自带文字。</param>
    /// <param name="parent">父节点。</param>
    /// <param name="resetScale">
    /// 是否把根 localScale 复位为 1。CommonButton 根被 art-kit 返工烤成了 3,多数面板复位后再用真实 sizeDelta(所见即所得);
    /// 但 StartPanel「准备」键与黄色侧栏键依赖这 3 倍缩放,须传 false 保持现状。
    /// </param>
    /// <param name="fontSize">非 null 时用 <c>UITheme.Font</c> 设置字号;null 保留预制体默认字号。</param>
    public static Button Build(GameObject prefab, string name, string label, Transform parent,
        bool resetScale = true, float? fontSize = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.SetActive(true);
        if (resetScale) ((RectTransform)go.transform).localScale = Vector3.one;
        SetLabel(go, label, fontSize);
        return go.GetComponent<Button>();
    }

    /// <summary>设置按钮内 TMP 文字(label 为 null 时不改);fontSize 非 null 时用 UITheme.Font 设字号。</summary>
    public static void SetLabel(GameObject buttonGo, string label, float? fontSize = null)
    {
        var text = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null) return;
        if (label != null) text.text = label;
        if (fontSize.HasValue) text.fontSize = UITheme.Font(fontSize.Value);
    }

    /// <summary>给 CommonButtonYellow 的 "Image" 子物体设置图标 sprite(可选染色);icon 为 null 时不动。</summary>
    public static void SetIcon(GameObject buttonGo, Sprite icon, Color? iconColor = null)
    {
        if (icon == null) return;
        var imageTr = buttonGo.transform.Find("Image");
        var img = imageTr != null ? imageTr.GetComponent<Image>() : null;
        if (img == null) return;
        img.sprite = icon;
        if (iconColor.HasValue) img.color = iconColor.Value;
    }
}
#endif
