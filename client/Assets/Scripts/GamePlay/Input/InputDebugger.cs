using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 临时调试组件：拖到 GameStart 场景任意 GameObject 上即可。
/// 屏幕左上角面板实时显示 InputService 状态 + 最近事件流。
///
/// 快捷键（直接读 Input.*，不走 InputService，避免被 IsEnabled 误伤）：
///   F1 = 显隐面板
///   F2 = 切换鼠标锁
///   F3 = 切换 InputService.IsEnabled（用于验证 disable 后事件确实不再触发）
///
/// 验收用，不要进发布构建——加 #if UNITY_EDITOR 或挂条件编译都行。
/// </summary>
public class InputDebugger : MonoBehaviour
{
    private const int EventLogCapacity = 20;

    private InputService input;
    private readonly Queue<string> eventLog = new Queue<string>(EventLogCapacity);
    private bool show = true;
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;

    private void Start()
    {
        if (GameLoop.Instance == null || GameLoop.Instance.Ctx == null)
        {
            Debug.LogError("[InputDebugger] GameLoop.Ctx 未就绪，组件停用");
            enabled = false;
            return;
        }

        if (!GameLoop.Instance.Ctx.TryGet<InputService>(out input))
        {
            Debug.LogError("[InputDebugger] InputService 未注册");
            enabled = false;
            return;
        }

        input.OnFireDown += HandleFireDown;
        input.OnFireUp += HandleFireUp;
        input.OnAimDown += HandleAimDown;
        input.OnAimUp += HandleAimUp;
        input.OnSprintDown += HandleSprintDown;
        input.OnSprintUp += HandleSprintUp;
        input.OnReloadDown += HandleReloadDown;
        input.OnScroll += HandleScroll;
    }

    private void OnDestroy()
    {
        if (input == null) return;
        input.OnFireDown -= HandleFireDown;
        input.OnFireUp -= HandleFireUp;
        input.OnAimDown -= HandleAimDown;
        input.OnAimUp -= HandleAimUp;
        input.OnSprintDown -= HandleSprintDown;
        input.OnSprintUp -= HandleSprintUp;
        input.OnReloadDown -= HandleReloadDown;
        input.OnScroll -= HandleScroll;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) show = !show;
        if (Input.GetKeyDown(KeyCode.F2) && input != null)
        {
            input.SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }
        if (Input.GetKeyDown(KeyCode.F3) && input != null)
        {
            input.IsEnabled = !input.IsEnabled;
            PushEvent($"[Debug] IsEnabled → {input.IsEnabled}");
        }
    }

    private void HandleFireDown() => PushEvent("Fire ↓");
    private void HandleFireUp() => PushEvent("Fire ↑");
    private void HandleAimDown() => PushEvent("Aim ↓");
    private void HandleAimUp() => PushEvent("Aim ↑");
    private void HandleSprintDown() => PushEvent("Sprint ↓");
    private void HandleSprintUp() => PushEvent("Sprint ↑");
    private void HandleReloadDown() => PushEvent("Reload");
    private void HandleScroll(float v) => PushEvent($"Scroll {v:+0.00;-0.00}");

    private void PushEvent(string msg)
    {
        if (eventLog.Count >= EventLogCapacity) eventLog.Dequeue();
        eventLog.Enqueue($"[{Time.frameCount}] {msg}");
    }

    private void OnGUI()
    {
        if (!show || input == null) return;

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 10, 10),
                fontSize = 13,
            };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        }

        const float W = 360f, H = 500f;
        GUI.Box(new Rect(10, 10, W, H),
            "InputService Debug    F1 hide  /  F2 cursor  /  F3 disable",
            boxStyle);

        float y = 38f;
        DrawLine(ref y, $"IsEnabled       : {input.IsEnabled}");
        DrawLine(ref y, $"LookSensitivity : {input.LookSensitivity:F2}");
        DrawLine(ref y, $"Cursor          : {Cursor.lockState}");
        DrawSep(ref y);
        DrawLine(ref y, $"Move        : ({input.Move.x:+0.00;-0.00}, {input.Move.y:+0.00;-0.00})");
        DrawLine(ref y, $"LookDelta   : ({input.LookDelta.x:+0.00;-0.00}, {input.LookDelta.y:+0.00;-0.00})");
        DrawLine(ref y, $"ScrollDelta : {input.ScrollDelta:+0.00;-0.00}");
        DrawSep(ref y);
        DrawLine(ref y, $"Fire   {Mark(input.FireHeld)}    Aim    {Mark(input.AimHeld)}");
        DrawLine(ref y, $"Sprint {Mark(input.SprintHeld)}");
        DrawSep(ref y);
        DrawLine(ref y, $"Events (latest {EventLogCapacity}):");
        foreach (var e in eventLog)
        {
            DrawLine(ref y, "  " + e);
        }
    }

    private void DrawLine(ref float y, string text)
    {
        GUI.Label(new Rect(22, y, 340, 18), text, labelStyle);
        y += 16f;
    }

    private void DrawSep(ref float y)
    {
        y += 4f;
        GUI.Label(new Rect(22, y, 340, 1), "────────────────────────────────────", labelStyle);
        y += 12f;
    }

    private static string Mark(bool b) => b ? "■" : "□";
}
