
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartScene : YSceneBase
{
    public override YSceneType SceneType
    {
        get { return YSceneType.Home; }
    }

    public override string SceneName
    {
        get { return "GameStartScene"; }
    }

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        UI.Hide<StartPanel>();      
        var manager = Context.Get<CharacterManager>();
        manager.GenneratePlayer();
        var bag = Context.Get<BagSystem>();
        // 把配表里所有道具各放入背包(可叠加的给多个,演示堆叠;不可叠加的各 1 个)
        bag.AddItem(1001, 5);   // 小型治疗药水(可叠)
        bag.AddItem(1002, 3);   // 大型治疗药水 1×2(可叠)
        bag.AddItem(1003, 5);   // 能量饮料(可叠)
        bag.AddItem(2001);      // 铁剑 1×3
        bag.AddItem(2002);      // 铁甲 2×2
        bag.AddItem(3001, 80);  // 铁矿石(可叠,超单堆上限会自动分多堆)
        bag.AddItem(3002, 30);  // 木材 2×1(可叠)
        bag.AddItem(4001);      // 远古信物 L 形
        bag.AddItem(4002);      // 曲柄扳手 T 形
        bag.AddItem(5001, 500); // 金币(可叠)
        UI.Show<BagPanel>();
    }

    protected override void OnEnterScene()
    {  
        EnterSceneComplete();
    }
    protected override void OnLeaveScene()
    {
        LeaveSceneComplete();
    }
}
