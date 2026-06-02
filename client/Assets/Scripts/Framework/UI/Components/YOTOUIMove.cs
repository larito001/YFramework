using UnityEngine;
using DG.Tweening;

public class YOTOUIMove : YOTOUIChangeBase
{
    [Header("位置控制")]
    public RectTransform StartPos;
    public RectTransform EndPos;

    [Header("过渡配置")]
    public float enterDuration = 0.5f;
    public float exitDuration = 0.5f;
    public Ease easeType = Ease.OutQuad;

    private RectTransform target;
    private Tween currentTween;

    void Awake()
    {
        target = GetComponent<RectTransform>();
        if (target == null)
        {
            Debug.LogError("YOTOUIMove需要挂在一个带RectTransform的UI对象上！");
        }
    }

    /// <summary>
    /// 播放进入动画（Start → End）
    /// </summary>
    public override void OnEnter()
    {
        PlayTween(StartPos, EndPos,enterDuration);
    }

    /// <summary>
    /// 播放退出动画（End → Start）
    /// </summary>
    public override void OnExist()
    {
        PlayTween(EndPos, StartPos, exitDuration);
    }

    private void PlayTween(RectTransform from, RectTransform to,float duration)
    {
        if (target == null || from == null || to == null) return;

        // 杀死之前的Tween
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }

        // 先设置到起始位置
        target.anchoredPosition = from.anchoredPosition;

        // 播放移动动画
        currentTween = target.DOAnchorPos(to.anchoredPosition, duration)
            .SetEase(easeType);
    }

    // 销毁前杀掉在飞的 tween:否则面板被销毁后 DOTween 仍会去 startup 已销毁的 RectTransform,触发安全模式告警。
    private void OnDestroy()
    {
        currentTween?.Kill();
    }
}