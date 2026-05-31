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

    private Tweener currentTween;

    public override void OnEnter()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) return;

        currentTween?.Kill();


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
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
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
}