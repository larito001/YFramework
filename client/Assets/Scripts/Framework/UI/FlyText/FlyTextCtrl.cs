using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 单条飘字实体。挂在 UI/FlyText/FlyTextPrefab 上。
/// 创建后由 FlyTextMgr 通过 Bind 注入回收回调，动画结束自动回池。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FlyTextCtrl : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmp;

    private RectTransform rct;
    private CanvasGroup cg;
    private Action<FlyTextCtrl> recycleCallback;
    private Vector3 currentPos;

    private void Awake()
    {
        rct = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }
        if (tmp == null)
        {
            tmp = GetComponent<TextMeshProUGUI>();
        }
    }

    public void Bind(Action<FlyTextCtrl> onRecycle)
    {
        recycleCallback = onRecycle;
    }

    public void Fly(FlyTextData data)
    {
        currentPos = data.pos;
        tmp.text = data.text;

        switch (data.flyTextType)
        {
            case FlyTextType.Normal:
                tmp.color = Color.white;
                StartAnim(0.5f, 1.2f, 1.0f, 0.4f, 0.1f, 1, 1, Ease.OutQuad, Ease.InOutQuad, false);
                break;
            case FlyTextType.Quick:
                tmp.color = Color.red;
                StartAnim(0.3f, 1.5f, 1.5f, 0.3f, 0.1f, 0.6f, 0.2f, Ease.OutElastic, Ease.OutBack, true);
                break;
            case FlyTextType.PlayerHurt:
                tmp.color = Color.red;
                StartAnim(0.5f, 1.2f, 1.0f, 0.4f, 0.1f, 1, 1, Ease.OutQuad, Ease.InOutQuad, true);
                break;
            case FlyTextType.AddHP:
                tmp.color = new Color(0.5f, 1, 0);
                StartAnim(0.5f, 1.2f, 1.0f, 0.4f, 0.1f, 1, 1, Ease.OutQuad, Ease.InOutQuad, true);
                break;
        }
    }

    private void StartAnim(float startScale, float maxScale, float lastScale,
        float toMaxDuration, float toLastDuration,
        float upTime, float fadeTime,
        Ease upAnim, Ease downAnim, bool useBurst)
    {
        rct.position = currentPos;
        rct.localScale = Vector3.one * startScale;
        cg.alpha = 1f;

        DOTween.Kill(rct);
        DOTween.Kill(cg);

        Sequence seq = DOTween.Sequence();

        Vector2 burstDir = Random.insideUnitCircle.normalized;
        burstDir.y = Mathf.Abs(burstDir.y);
        Vector3 burstOffset = Vector3.zero;
        if (useBurst)
        {
            burstOffset = new Vector3(burstDir.x, burstDir.y, 0) * Random.Range(40f, 80f);
        }

        Vector3 burstTarget = currentPos + burstOffset;
        Vector3 finalTarget = burstTarget + new Vector3(0, Random.Range(40f, 80f), 0);

        float burstTime = toMaxDuration;
        float floatTime = upTime - burstTime;

        seq.Join(rct.DOScale(maxScale, burstTime).SetEase(upAnim));
        seq.Join(rct.DOMove(burstTarget, burstTime).SetEase(Ease.OutCubic));
        seq.Append(rct.DOScale(lastScale, toLastDuration).SetEase(downAnim));
        seq.Join(rct.DOMove(finalTarget, floatTime).SetEase(Ease.OutSine));
        seq.Join(cg.DOFade(0f, fadeTime).SetEase(Ease.InQuad));
        seq.OnComplete(AnimComplete);
    }

    private void AnimComplete()
    {
        recycleCallback?.Invoke(this);
    }
}
