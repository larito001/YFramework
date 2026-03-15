
using System;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

/// <summary>
/// 管理角色
/// </summary>
public class PlayerManager : IGameService,ITickable
{
    private PlayerEntity _player;
    private SceneReferenceService _sceneReferenceService;
    public CameraMgr CameraMgr { get; private set; }
    public PlayerEntity Player => _player;
    /// <summary>
    /// 首次生成角色
    /// </summary>
    public void GeneratePlayer()
    {
        if (_player != null)
        {
            return;
        }

        _player = PlayerEntity.pool.GetItem(this);
        if (_sceneReferenceService.TryGetTransform(SceneReferenceKeys.PlayerSpawn, out var spawnPoint))
        {
            _player.Location = spawnPoint.position;
        }
        else
        {
            Debug.LogWarning($"Player spawn point '{SceneReferenceKeys.PlayerSpawn}' was not found.");
        }
    }

    public void ClearPlayer()
    {
        if (_player == null)
        {
            return;
        }

        PlayerEntity.pool.RecoverItem(_player);
        _player = null;
    }
    /// <summary>
    ///角色死亡 
    /// </summary>
    public void OnPlayerDie()
    {
        
    } 
    

    public void Init(GameContext ctx)
    {
        CameraMgr = ctx.Get<CameraMgr>();
        _sceneReferenceService = ctx.Get<SceneReferenceService>();
    }

    public void Shutdown()
    {
        ClearPlayer();
    }

    public void Tick(float dt)
    {
        if (_player != null)
        {        _player.Tick(dt);
            
        }

    }
}
