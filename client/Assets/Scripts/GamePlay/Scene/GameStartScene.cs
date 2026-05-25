using UnityEngine;
using YOTO;

/// <summary>
/// 挂在 GameStartScene.unity 根节点上的引导脚本。
/// Start 阶段所有 Awake 已结束，GameLoop.Ctx 必已就绪。
/// </summary>
public class GameStartScene : MonoBehaviour
{
    private void Start()
    {
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx == null)
        {
            return;
        }

        ctx.Get<UIMgr>().Hide<StartPanel>();
    }
}
