using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class GameMapPanel : UIPageBase
{
    [SerializeField]
    private List<Button> levelButtonList = new List<Button>();
    public override void OnLoad()
    {
     
    }

    public override void OnShow()
    {
        for (var i = 0; i < levelButtonList.Count; i++)
        {
            var button = levelButtonList[i];
            button.onClick.AddListener(() =>
            {
                YOTOFramework.uIMgr.Show(UIEnum.LoadingPanel);
                YOTOFramework.uIMgr.Show(UIEnum.GameMainPanel);
            });
        } 
    }

    public override void OnHide()
    {
        for (var i = 0; i < levelButtonList.Count; i++)
        {
            var button = levelButtonList[i];
            button.onClick.RemoveAllListeners();
        }
    }

    public override void OnResize()
    {
        
    }
}
