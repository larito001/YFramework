using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按键设置页签:列出可重绑定动作(<see cref="InputService.RebindableActions"/>),点某行的键按钮进入"按任意键"捕获,
/// 捕获到即写入 <see cref="InputService.SetBinding"/>(立即生效并存盘)。Esc 取消,"恢复默认"一键还原。
/// </summary>
public class KeybindingTab : SettingTabBase
{
    private static readonly Dictionary<InputAction, string> DisplayNames = new Dictionary<InputAction, string>
    {
        { InputAction.Sprint, "冲刺" },
        { InputAction.Reload, "换弹" },
        { InputAction.InteractWorld, "世界交互" },
        { InputAction.ToggleBag, "背包" },
        { InputAction.Melee, "近战" },
    };

    private readonly List<(InputAction action, TextMeshProUGUI label, Button button)> rows = new List<(InputAction, TextMeshProUGUI, Button)>();
    private InputAction? capturing;

    protected override void Build()
    {
        NewLabel(Content, "按键设置", 400, 30, TextAlignmentOptions.Left);

        foreach (var action in InputService.RebindableActions)
        {
            var captured = action;
            var row = NewRow(Content);
            NewLabel(row.transform, DisplayNames.TryGetValue(action, out var n) ? n : action.ToString(), 200, 24, TextAlignmentOptions.Left);
            var btn = NewButton(row.transform, "-", 220, 44);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            btn.onClick.AddListener(() => BeginCapture(captured));
            rows.Add((action, label, btn));
        }

        var resetRow = NewRow(Content, 64);
        NewButton(resetRow.transform, "恢复默认", 220, 52).onClick.AddListener(() =>
        {
            CancelCapture();
            Resolve<InputService>()?.ResetBindings();
        });
    }

    protected override void Refresh()
    {
        var input = Resolve<InputService>();
        if (input == null) return;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (capturing == r.action) continue; // 正在捕获的那行保持提示
            r.label.text = input.GetBinding(r.action).ToString();
        }
    }

    protected override void Bind()
    {
        var input = Resolve<InputService>();
        if (input != null) input.BindingsChanged += Refresh; // 重置/外部改键时同步标签
    }

    protected override void Unbind()
    {
        var input = Resolve<InputService>();
        if (input != null) input.BindingsChanged -= Refresh;
        CancelCapture();
    }

    private void Update()
    {
        if (capturing == null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCapture();
            return;
        }

        // 捕获按下的第一个键。遍历全部 KeyCode,跳过鼠标键(留给固定的开火/瞄准)。
        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) continue;
            if (Input.GetKeyDown(key))
            {
                Resolve<InputService>()?.SetBinding(capturing.Value, key);
                capturing = null;
                Refresh();
                return;
            }
        }
    }

    private void BeginCapture(InputAction action)
    {
        capturing = action;
        Refresh();
        foreach (var r in rows)
        {
            if (r.action == action) r.label.text = "按任意键... (Esc 取消)";
        }
    }

    private void CancelCapture()
    {
        if (capturing == null) return;
        capturing = null;
        Refresh();
    }
}
