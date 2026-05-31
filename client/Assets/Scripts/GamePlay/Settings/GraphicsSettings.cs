using System;
using UnityEngine;
using YOTO;

/// <summary>
/// 画面设置服务:全屏 / 垂直同步 / 画质等级 / 分辨率。改动即应用并存盘(走 StoreMgr 的 Settings 分类,全局不随存档槽)。
/// UI(<see cref="GraphicsSettingsTab"/>)只调本服务,不直接碰 Unity 的 Screen/QualitySettings。
/// </summary>
public class GraphicsSettings : IGameService
{
    [Serializable]
    public class Data
    {
        public bool fullscreen = true;
        public bool vSync = true;
        public int qualityLevel = -1; // -1 = 启动时取当前
        public int resWidth;          // 0 = 启动时取当前
        public int resHeight;
    }

    private Data data = new Data();
    private StoreMgr store;
    private ISaveHandle handle;

    public Data Current => data;
    public string[] QualityNames => QualitySettings.names;

    public void Init(GameContext ctx)
    {
        store = ctx.Get<StoreMgr>();
        Normalize(); // 先用当前运行环境填默认,读档没有时即为这套
        handle = store.Register("graphics_settings",
            () => data,
            (Data d) => { data = d; Normalize(); Apply(); },
            SaveCategory.Settings);
        handle.Load();
    }

    public void Shutdown()
    {
        store = null;
        handle = null;
    }

    // ---------------- 对外设置(UI 调用)----------------

    // 每个 setter 只做自己那一项,避免改画质/垂直同步时也跟着重设分辨率(会触发屏幕重置/闪烁)。
    public void SetFullscreen(bool v)
    {
        data.fullscreen = v;
        Screen.SetResolution(data.resWidth, data.resHeight, v);
        Save();
    }

    public void SetVSync(bool v)
    {
        data.vSync = v;
        QualitySettings.vSyncCount = v ? 1 : 0;
        Save();
    }

    public void SetQualityLevel(int level)
    {
        data.qualityLevel = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(data.qualityLevel, true);
        Save();
    }

    public void SetResolution(int width, int height)
    {
        data.resWidth = width; data.resHeight = height;
        Screen.SetResolution(width, height, data.fullscreen);
        Save();
    }

    // ---------------- 内部 ----------------

    private void Normalize()
    {
        if (data == null) data = new Data();
        int qCount = QualitySettings.names.Length;
        if (data.qualityLevel < 0 || data.qualityLevel >= qCount) data.qualityLevel = QualitySettings.GetQualityLevel();
        if (data.resWidth <= 0 || data.resHeight <= 0) { data.resWidth = Screen.width; data.resHeight = Screen.height; }
    }

    public void Apply()
    {
        QualitySettings.SetQualityLevel(Mathf.Clamp(data.qualityLevel, 0, QualitySettings.names.Length - 1), true);
        QualitySettings.vSyncCount = data.vSync ? 1 : 0;
        Screen.SetResolution(data.resWidth, data.resHeight, data.fullscreen);
    }

    private void Save() => handle?.Save();
}
