using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置页签基类:挂在三个页签容器上,激活时懒构建自己的控件并绑定到对应服务。
/// 控件在运行时构建(而非全塞进预制体),每个页签的 UI 和逻辑就近放一起,易维护。
/// 字体由生成器写入 <see cref="font"/>(中文需要 SIMHEI SDF)。
/// </summary>
public abstract class SettingTabBase : MonoBehaviour
{
    public TMP_FontAsset font;

    private bool built;

    /// <summary>子类把控件加到这里(滚动内容容器),而非直接加到自身。</summary>
    protected RectTransform Content { get; private set; }

    protected T Resolve<T>() where T : class
        => GameLoop.Instance != null && GameLoop.Instance.Ctx != null ? GameLoop.Instance.Ctx.Get<T>() : null;

    protected virtual void OnEnable()
    {
        if (!built)
        {
            SetupRoot();
            Build();
            built = true;
        }
        Refresh();
        Bind();
    }

    protected virtual void OnDisable() => Unbind();

    /// <summary>构建本页签控件(只调一次)。</summary>
    protected abstract void Build();

    /// <summary>从服务拉最新值刷新显示(每次激活调用)。</summary>
    protected virtual void Refresh() { }
    protected virtual void Bind() { }
    protected virtual void Unbind() { }

    // ============================ 运行时 UI 工具 ============================

    private void SetupRoot()
    {
        // 容器做成 ScrollRect:内容超出可视区(如按键 8 行)时纵向滚动,不会向下溢出压到返回按钮。
        var scroll = gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var viewport = NewUI("Viewport", transform);
        var vpRt = (RectTransform)viewport.transform;
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one; vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // 透明底,空白处也能接拖拽滚动

        var contentGo = NewUI("Content", viewport.transform);
        var contentRt = (RectTransform)contentGo.transform;
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.padding = new RectOffset(28, 28, 24, 24);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRt;
        scroll.content = contentRt;
        Content = contentRt;
    }

    protected static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    protected TextMeshProUGUI NewText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = NewUI("Text", parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
        return tmp;
    }

    /// <summary>一行(水平布局),高度固定。</summary>
    protected GameObject NewRow(Transform parent, float height = 56)
    {
        var go = NewUI("Row", parent);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.padding = new RectOffset(8, 8, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        return go;
    }

    protected TextMeshProUGUI NewLabel(Transform parent, string text, float width, float size, TextAlignmentOptions align)
    {
        var t = NewText(parent, text, size, align);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        return t;
    }

    protected Image NewImage(Transform parent, string name, Color color)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    protected Button NewButton(Transform parent, string label, float width, float height, float fontSize = 22)
    {
        var go = NewUI("Button", parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width;
        le.minHeight = height; le.preferredHeight = height;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.32f, 0.4f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var lbl = NewText(go.transform, label, fontSize, TextAlignmentOptions.Center);
        Stretch(lbl.rectTransform);
        return btn;
    }

    protected Slider NewSlider(Transform parent)
    {
        var go = NewUI("Slider", parent);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1; le.minWidth = 240; le.preferredHeight = 28;
        var slider = go.AddComponent<Slider>();

        var bg = NewImage(go.transform, "Background", new Color(0.3f, 0.3f, 0.35f, 1f));
        var bgRt = bg.rectTransform; bgRt.anchorMin = new Vector2(0, 0.3f); bgRt.anchorMax = new Vector2(1, 0.7f); bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var fillArea = NewUI("Fill Area", go.transform); var faRt = (RectTransform)fillArea.transform;
        faRt.anchorMin = new Vector2(0, 0.25f); faRt.anchorMax = new Vector2(1, 0.75f); faRt.offsetMin = new Vector2(6, 0); faRt.offsetMax = new Vector2(-6, 0);
        var fill = NewImage(fillArea.transform, "Fill", new Color(0.4f, 0.6f, 0.9f, 1f));
        var fillRt = fill.rectTransform; fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(0, 1); fillRt.sizeDelta = new Vector2(10, 0);

        var handleArea = NewUI("Handle Slide Area", go.transform); var haRt = (RectTransform)handleArea.transform;
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one; haRt.offsetMin = new Vector2(6, 0); haRt.offsetMax = new Vector2(-6, 0);
        var handle = NewImage(handleArea.transform, "Handle", Color.white);
        var handleRt = handle.rectTransform; handleRt.sizeDelta = new Vector2(20, 0);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        return slider;
    }

    protected Toggle NewToggle(Transform parent)
    {
        var go = NewUI("Toggle", parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 36; le.preferredWidth = 36; le.preferredHeight = 36;
        var toggle = go.AddComponent<Toggle>();

        var bg = NewImage(go.transform, "Background", new Color(0.25f, 0.27f, 0.32f, 1f));
        var bgRt = bg.rectTransform; bgRt.anchorMin = new Vector2(0, 0.5f); bgRt.anchorMax = new Vector2(0, 0.5f); bgRt.pivot = new Vector2(0, 0.5f);
        bgRt.sizeDelta = new Vector2(32, 32); bgRt.anchoredPosition = Vector2.zero;
        var check = NewImage(bg.transform, "Checkmark", new Color(0.4f, 0.85f, 0.45f, 1f));
        var cRt = check.rectTransform; cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f); cRt.sizeDelta = new Vector2(20, 20); cRt.anchoredPosition = Vector2.zero;

        toggle.targetGraphic = bg;
        toggle.graphic = check;
        return toggle;
    }

    protected static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
