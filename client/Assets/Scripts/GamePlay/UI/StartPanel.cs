using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class StartPanel : UIPageBase
{
    public Button btn_new;
    public Button btn_continue;
    public Button btn_setting;

    public override void OnLoad()
    {
        btn_new.onClick.AddListener(OnNewClick);
        btn_setting.onClick.AddListener(OnSettingClick);
    }

    private void OnSettingClick()
    {
        YFramework.uIMgr.Show(UIEnum.SettingPanel);
    }

    private void OnNewClick()
    {
        YFramework.uIMgr.Show(UIEnum.LoadingPanel);
        Timers.inst.Add(1.5f, (o) =>
        {
            CloseSelf();
            PlayerEntity playerEntity = PlayerEntity.pool.GetItem(null);
            playerEntity.Location = GameStarter.PlayerOrgPos.position;
            YFramework.uIMgr.Show(UIEnum.GameMainPanel);
            EnemiesManager.instance.SetPlayer(playerEntity);
            for (int i = 0; i < 20; i++)
            {
                // todo：在范围内随机生成
                Vector3 basePos = playerEntity.Location;
                float randomX = Random.Range(-50f,50f);
                float randomZ =  Random.Range(-50f, 50f);
                float randomY = 20f;

                Vector3 spawnPos = basePos + new Vector3(randomX, randomY, randomZ);

                EnemiesManager.instance.GenerateEnemyAt(spawnPos);
            }
        });
        Timers.inst.Add(5, (o) => { YFramework.uIMgr.Hide(UIEnum.LoadingPanel); });
  
    }

    public override void OnShow()
    {
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}