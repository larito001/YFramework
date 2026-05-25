using UnityEngine;

/// <summary>
/// 挂在 GameMainScene.unity 根节点上的引导脚本。
/// 玩法场景的初始化（UI 显示、敌人生成等）放在此处的 Start。
/// </summary>
public class GameMainScene : MonoBehaviour
{
    private void Start()
    {
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx == null)
        {
            return;
        }
    }
}
