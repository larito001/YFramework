using System.Collections;
using System.Collections.Generic;
using Codice.CM.Common;
using UnityEngine;
using YOTO;

public class CardPlugin : LogicPluginBase
{
    private Dictionary<int, int> typeCount;


    List<CardVisual> cardVisuals = new List<CardVisual>();
    public static CardPlugin Instance;

    public CardPlugin()
    {
        Instance = this;
    }

    private GameObject cards;

    protected override void OnInstall()
    {
        base.OnInstall();
    }

    protected override void OnUninstall()
    {
        base.OnUninstall();
    }
    HorizontalCardHolder holder;

    public void RemoveCard(Card card)
    {
        holder.RemoveCard(card);
    }
    public void AddCard(CardVisual visual)
    {
        cardVisuals.Add(visual);
        CheckIsDouble();
    }

    public int finishCount = 0;
    private void CheckIsDouble()
    {
        if (cardVisuals.Count == 2)
        {
            var card1 = cardVisuals[0];
            var card2 = cardVisuals[1];
            if (card1!=card2&&card1.CardType == card2.CardType)
            {
                RemoveCard(   card1.parentCard);
                RemoveCard(card2.parentCard);
                holder.AddCard(card1.CardType );
                finishCount++;
                if (finishCount >= 3)
                {
                    YOTOFramework.uIMgr.Show(UIEnum.FinishPanel);
                }
            }
            else
            {
                FlyTextMgr.Instance.AddTextAtScreenCenter("匹配失败");
            }
            cardVisuals.Clear();
        }
    }

    public void HideCards()
    {
        if (cards == null)
        {
            cards = GameObject.Find("CardQuickStartPrefab");
            holder = cards.GetComponentInChildren<HorizontalCardHolder>();
     
        }

        cards.SetActive(false);
    }

    public void ShowCards()
    {
        typeCount = new Dictionary<int, int>()
        {
            { 1, 2 }, { 2, 2 }, { 3, 2 }
        };
        finishCount = 0;
        if (cards == null)
        {
            cards = GameObject.Find("CardQuickStartPrefab");
            holder = cards.GetComponentInChildren<HorizontalCardHolder>();
        }

        cards.SetActive(true);
        holder.Init();
    }
    public int RandomSelect()
    {
        // 创建可用的key列表
        List<int> availableKeys = new List<int>();
        
        // 遍历字典，找出所有value大于0的key
        foreach (KeyValuePair<int, int> pair in typeCount)
        {
            if (pair.Value > 0)
            {
                availableKeys.Add(pair.Key);
            }
        }

        // 检查是否有可用的key
        if (availableKeys.Count == 0)
        {
            Debug.LogError("没有可用的key了！");
            return -1;
        }

        // 随机选择一个key
        int randomIndex = Random.Range(0, availableKeys.Count);
        int selectedKey = availableKeys[randomIndex];

        // 对应的value减1
        typeCount[selectedKey] = typeCount[selectedKey] - 1;

        Debug.Log("抽中: " + selectedKey + ", 剩余次数: " + typeCount[selectedKey]);
        
        return selectedKey;
    }
}