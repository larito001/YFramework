using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 读取存档界面:列出所有存档槽(<see cref="StoreMgr.Slots"/>),点某行载入并进入 Home,右侧删除该槽,返回回开始界面。
/// 行由 <see cref="rowTemplate"/> 运行时克隆。存档槽清单是异步读入的,故 OnShow 用 <see cref="StoreMgr.WhenSlotsReady"/> 等就绪再刷新。
/// </summary>
public class SaveSlotPanel : UIPageBase
{
    public Button btn_back;
    public RectTransform content;     // 滚动内容容器(挂 VerticalLayoutGroup)
    public SaveSlotRow rowTemplate;   // 行模板(默认隐藏)
    public GameObject emptyHint;      // "暂无存档"提示

    private StoreMgr store;
    private bool busy; // 防止载入切场景前重复点击
    private readonly List<SaveSlotRow> spawned = new List<SaveSlotRow>();

    public override void OnLoad()
    {
        store = GetService<StoreMgr>();
        btn_back.onClick.AddListener(CloseSelf);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
    }

    public override void OnShow()
    {
        busy = false;
        store?.WhenSlotsReady(Refresh); // 清单就绪后(或已就绪立即)刷新列表
    }

    private void Refresh()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        }
        spawned.Clear();

        var slots = store != null ? store.Slots : null;
        int count = slots != null ? slots.Count : 0;
        if (emptyHint != null) emptyHint.SetActive(count == 0);
        if (rowTemplate == null || content == null) return;

        for (int i = 0; i < count; i++)
        {
            var info = slots[i];
            int slotId = info.id; // 捕获 id,删除/移位后仍正确
            var row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Bind(info, i, () => OnLoadSlot(slotId), () => OnDeleteSlot(slotId));
            spawned.Add(row);
        }
    }

    private void OnLoadSlot(int slotId)
    {
        if (busy) return;
        busy = true;
        store.SetActiveSlot(slotId);
        store.LoadAll(() =>
        {
            CloseSelf(); // 读档完成,先关掉读档界面再切场景
            GetService<YSceneManager>().SwitchScene(YSceneType.Home);
        });
    }

    private void OnDeleteSlot(int slotId)
    {
        store.DeleteSlot(slotId);
        Refresh();
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}
