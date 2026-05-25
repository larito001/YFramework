using Mirror;
using UnityEngine;

/// <summary>
/// 联网玩家示例。挂在 Player prefab 上，prefab 还需要：
///   - NetworkIdentity 组件（Mirror 必需）
///   - 任意 NetworkTransform 实现（推荐 NetworkTransformReliable）做位置同步
///   - Collider（如果想被点击/拖拽，配合 SceneInteractionService）
/// prefab 配置到 NetworkManager.playerPrefab 字段，Mirror 在 OnServerAddPlayer 时自动 spawn。
/// </summary>
public class NetworkPlayer : NetworkEntityBase
{
    [SyncVar] public int playerId;

    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        if (!isLocalPlayer) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        if (h == 0f && v == 0f) return;

        transform.position += new Vector3(h, 0, v) * (moveSpeed * Time.deltaTime);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log($"[NetworkPlayer] Local player started, netId={netId}");
    }
}
