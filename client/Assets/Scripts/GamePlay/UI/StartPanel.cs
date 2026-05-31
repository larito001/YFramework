using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class StartPanel : UIPageBase
{
    public Button btn_new;
    public Button btn_continue;
    public Button btn_setting;
    public Button btn_quit;

    private StoreMgr store;
    private bool busy; // 防止切场景前重复点击

    public override void OnLoad()
    {
        store = GetService<StoreMgr>();
        btn_new.onClick.AddListener(OnNewClick);
        btn_continue.onClick.AddListener(OnContinueClick);
        btn_setting.onClick.AddListener(OnSettingClick);
        btn_quit.onClick.AddListener(OnQuitClick);
    }

    // 新游戏:新建一个空存档槽并设为激活,LoadAll 把各进度系统重置为初始(空槽无文件 → restore 收 new T()),然后进 Home。
    private void OnNewClick()
    {
        if (busy) return;
        busy = true;
        if (store == null)
        {
            GetService<YSceneManager>().SwitchScene(YSceneType.Home);
            return;
        }
        store.WhenSlotsReady(() =>
        {
            store.CreateSlot(); // 新槽即激活;设置(Settings)全局不受影响
            store.LoadAll(() => GetService<YSceneManager>().SwitchScene(YSceneType.Home));
        });
    }

    // 读取存档:打开存档选择界面(无存档时此按钮已在 OnShow 置灰)。
    private void OnContinueClick()
    {
        Show<SaveSlotPanel>();
    }

    private void OnSettingClick()
    {
        Show<SettingPanel>();
    }

    private void OnQuitClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public override void OnShow()
    {
        busy = false;
        // 无任何存档槽时禁用「读取存档」(存档槽清单异步读入,就绪后再判定)。
        if (btn_continue != null)
        {
            btn_continue.interactable = false;
            store?.WhenSlotsReady(() =>
            {
                if (btn_continue != null) btn_continue.interactable = store.Slots.Count > 0;
            });
        }
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}
