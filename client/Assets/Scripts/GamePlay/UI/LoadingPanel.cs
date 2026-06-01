using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 加载页(竖屏):首次启动读档 / 场景切换时由 <see cref="UIMgr.ShowLoading"/> 显示,RayCast 层全屏覆盖、挡输入。
/// 视觉:深色全屏背景 + 居中标题 + 旋转转圈 + "加载中…"(省略号循环) + 底部小贴士(每次随机一条)。
/// 纯展示无业务逻辑;外部读/写完成调 <see cref="UIMgr.HideLoading"/> 收起(淡入淡出由 <see cref="YOTOUIShow"/> 处理)。
/// 用 unscaledDeltaTime 驱动动画,timeScale=0 时仍转。预制体由 <c>Tools/UI/Build LoadingPanel Prefab</c> 生成,字段在那里接好。
/// </summary>
public class LoadingPanel : UIPageBase
{
    [Header("转圈(每帧绕 Z 旋转)")]
    public RectTransform spinner;
    public float spinnerSpeed = 220f; // 度/秒(顺时针)

    [Header("文本")]
    public TextMeshProUGUI loadingText; // "加载中" + 循环省略号
    public TextMeshProUGUI tipText;     // 底部小贴士

    [Tooltip("小贴士池,显示时随机取一条")]
    public List<string> tips = new List<string>();

    private const string LoadingBase = "加载中";
    private const float DotInterval = 0.35f; // 省略号每跳间隔
    private float dotTimer;
    private int dotCount;

    public override void OnLoad() { }

    public override void OnShow()
    {
        dotTimer = 0f;
        dotCount = 0;
        if (loadingText != null) loadingText.text = LoadingBase;
        if (tipText != null && tips != null && tips.Count > 0)
            tipText.text = tips[Random.Range(0, tips.Count)];
    }

    public override void OnHide() { }
    public override void OnResize() { }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 转圈:匀速顺时针旋转
        if (spinner != null) spinner.Rotate(0f, 0f, -spinnerSpeed * dt);

        // "加载中" 后省略号 0→3 循环,给"仍在加载"的动态反馈
        if (loadingText != null)
        {
            dotTimer += dt;
            if (dotTimer >= DotInterval)
            {
                dotTimer -= DotInterval;
                dotCount = (dotCount + 1) % 4;
                loadingText.text = LoadingBase + new string('.', dotCount);
            }
        }
    }
}
