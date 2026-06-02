using UnityEngine;
using DG.Tweening;

public class YOTOUIShow : YOTOUIChangeBase
{
    private CanvasGroup canvasGroup;

    [Header("进入配置")] public bool useEnterAnim = true;
    public float enterDuration = 0.18f;
    public Ease enterEase = Ease.OutCubic;

    // 退出比进入更快、用 OutQuad(快起步):消失要干脆,不要拖。
    [Header("退出配置")] public bool useExitAnim = true;
    public float exitDuration = 0.1f;
    public Ease exitEase = Ease.OutQuad;

    // 统一弹出动画:进入时从略小缩放弹到原大(OutBack 带回弹),退出时缩回。所有界面挂同一组件即统一生效。
    [Header("弹出缩放(统一)")] public bool usePopScale = true;
    public float enterFromScale = 0.85f;        // 进入起始缩放(弹到 1)
    public Ease enterScaleEase = Ease.OutBack;   // 回弹:略过冲 1 再回落,得到"弹出"手感
    public float exitToScale = 0.9f;             // 退出目标缩放
    public Ease exitScaleEase = Ease.InQuad;

    private Tweener currentTween;
    private Tweener scaleTween;

    public override void OnEnter()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) return;

        currentTween?.Kill();
        scaleTween?.Kill();

        // 缩放弹出:与淡入同时进行(只动 localScale,绕枢轴缩放,对全屏 stretch 根节点同样适用)
        if (usePopScale)
        {
            transform.localScale = Vector3.one * enterFromScale;
            scaleTween = transform.DOScale(1f, enterDuration).SetEase(enterScaleEase);
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        if (useEnterAnim)
        {
            currentTween = canvasGroup.DOFade(1f, enterDuration)
                .SetEase(enterEase).OnComplete(() =>
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                });
        }
        else
        {
            canvasGroup.alpha = 1f;

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public override void OnExist()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) return;

        currentTween?.Kill();
        scaleTween?.Kill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (usePopScale)
            scaleTween = transform.DOScale(exitToScale, exitDuration).SetEase(exitScaleEase);

        if (useExitAnim)
        {
            currentTween = canvasGroup.DOFade(0f, exitDuration)
                .SetEase(exitEase);
        }
        else
        {
            canvasGroup.alpha = 0f;
        }
    }

    // 销毁前杀掉在飞的 tween:否则面板被销毁(切场景等)后,DOTween 下一帧仍会去 startup 已销毁的
    // CanvasGroup/RectTransform,触发安全模式告警 "object has been destroyed but you are still trying to access it"。
    private void OnDestroy()
    {
        currentTween?.Kill();
        scaleTween?.Kill();
    }
}