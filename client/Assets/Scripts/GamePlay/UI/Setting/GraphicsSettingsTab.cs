using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>画面设置页签:全屏 / 垂直同步 开关,画质 / 分辨率 左右切换。绑定 GraphicsSettings(改动即应用并存盘)。</summary>
public class GraphicsSettingsTab : SettingTabBase
{
    private Toggle fullscreenToggle;
    private Toggle vsyncToggle;
    private TextMeshProUGUI qualityValue;
    private TextMeshProUGUI resValue;

    private int qualityIndex;
    private int resIndex;
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    protected override void Build()
    {
        BuildResolutionList();

        NewLabel(transform, "画面设置", 400, 30, TextAlignmentOptions.Left);

        // 全屏
        var fsRow = NewRow(transform);
        NewLabel(fsRow.transform, "全屏", 160, 24, TextAlignmentOptions.Left);
        fullscreenToggle = NewToggle(fsRow.transform);
        fullscreenToggle.onValueChanged.AddListener(v => Resolve<GraphicsSettings>()?.SetFullscreen(v));

        // 垂直同步
        var vsRow = NewRow(transform);
        NewLabel(vsRow.transform, "垂直同步", 160, 24, TextAlignmentOptions.Left);
        vsyncToggle = NewToggle(vsRow.transform);
        vsyncToggle.onValueChanged.AddListener(v => Resolve<GraphicsSettings>()?.SetVSync(v));

        // 画质(用 < > 而非 ◀ ▶:SIMHEI SDF 字符集没有箭头符号,会回退到别的字体显示)
        var qRow = NewRow(transform);
        NewLabel(qRow.transform, "画质", 160, 24, TextAlignmentOptions.Left);
        NewButton(qRow.transform, "<", 56, 44).onClick.AddListener(() => StepQuality(-1));
        qualityValue = NewLabel(qRow.transform, "-", 220, 22, TextAlignmentOptions.Center);
        NewButton(qRow.transform, ">", 56, 44).onClick.AddListener(() => StepQuality(1));

        // 分辨率
        var rRow = NewRow(transform);
        NewLabel(rRow.transform, "分辨率", 160, 24, TextAlignmentOptions.Left);
        NewButton(rRow.transform, "<", 56, 44).onClick.AddListener(() => StepResolution(-1));
        resValue = NewLabel(rRow.transform, "-", 220, 22, TextAlignmentOptions.Center);
        NewButton(rRow.transform, ">", 56, 44).onClick.AddListener(() => StepResolution(1));
    }

    protected override void Refresh()
    {
        var gfx = Resolve<GraphicsSettings>();
        if (gfx == null) return;
        var d = gfx.Current;
        fullscreenToggle.SetIsOnWithoutNotify(d.fullscreen);
        vsyncToggle.SetIsOnWithoutNotify(d.vSync);

        qualityIndex = Mathf.Clamp(d.qualityLevel, 0, Mathf.Max(0, gfx.QualityNames.Length - 1));
        resIndex = FindResIndex(d.resWidth, d.resHeight);
        UpdateQualityText(gfx);
        UpdateResText();
    }

    private void StepQuality(int dir)
    {
        var gfx = Resolve<GraphicsSettings>();
        if (gfx == null || gfx.QualityNames.Length == 0) return;
        qualityIndex = Mathf.Clamp(qualityIndex + dir, 0, gfx.QualityNames.Length - 1);
        gfx.SetQualityLevel(qualityIndex);
        UpdateQualityText(gfx);
    }

    private void StepResolution(int dir)
    {
        var gfx = Resolve<GraphicsSettings>();
        if (gfx == null || resolutions.Count == 0) return;
        resIndex = Mathf.Clamp(resIndex + dir, 0, resolutions.Count - 1);
        var r = resolutions[resIndex];
        gfx.SetResolution(r.x, r.y);
        UpdateResText();
    }

    private void UpdateQualityText(GraphicsSettings gfx)
    {
        var names = gfx.QualityNames;
        qualityValue.text = (qualityIndex >= 0 && qualityIndex < names.Length) ? names[qualityIndex] : "-";
    }

    private void UpdateResText()
    {
        if (resIndex >= 0 && resIndex < resolutions.Count)
        {
            var r = resolutions[resIndex];
            resValue.text = $"{r.x} x {r.y}";
        }
        else
        {
            resValue.text = "-";
        }
    }

    private void BuildResolutionList()
    {
        resolutions.Clear();
        var seen = new HashSet<long>();
        foreach (var r in Screen.resolutions)
        {
            long key = ((long)r.width << 32) | (uint)r.height;
            if (seen.Add(key)) resolutions.Add(new Vector2Int(r.width, r.height));
        }
        if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height)); // 编辑器下可能为空
    }

    private int FindResIndex(int w, int h)
    {
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (resolutions[i].x == w && resolutions[i].y == h) return i;
        }
        return 0;
    }
}
