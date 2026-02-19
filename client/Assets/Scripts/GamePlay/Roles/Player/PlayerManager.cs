
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
    public CameraMgr CameraMgr { get; private set; }
    /// <summary>
    /// 首次生成角色
    /// </summary>
    public void GeneratePlayer()
    {
        _player =PlayerEntity.pool.GetItem(this);
        _player.Location = GameObject.Find("playerPos").transform.position;
    }

    public void ClearPlayer()
    {
        PlayerEntity.pool.RecoverItem(_player);
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
    }

    public void Shutdown()
    {
      
    }

    public void Tick(float dt)
    {
        if (_player != null)
        {        _player.Tick(dt);
            
        }

    }
}