using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 声音设置页签:音乐/音效 两路音量滑条 + 静音开关,绑定 SoundMgr(改动即生效并存盘)。
/// 控件由 <see cref="SettingPanelBuilder"/> 烤进预制体(不再运行时构建);本类只引用序列化字段做刷新/绑定。
/// 数组下标对应 <see cref="Channels"/>:0=音乐,1=音效。
/// </summary>
public class SoundSettingsTab : MonoBehaviour
{
    private static readonly SoundChannel[] Channels = { SoundChannel.Music, SoundChannel.Sfx };

    // 由生成器写入,长度与 Channels 一致(0=音乐,1=音效)。
    public Slider[] sliders = new Slider[Channels.Length];
    public TextMeshProUGUI[] values = new TextMeshProUGUI[Channels.Length];
    public Toggle[] mutes = new Toggle[Channels.Length];

    private bool wired;
    private bool refreshing;

    private void OnEnable()
    {
        WireOnce();
        Refresh();
    }

    /// <summary>给预制体里的控件挂一次回调(SetActive 反复触发 OnEnable 也只挂一次)。</summary>
    private void WireOnce()
    {
        if (wired) return;
        for (int i = 0; i < Channels.Length; i++)
        {
            int idx = i;
            if (sliders[i] != null) sliders[i].onValueChanged.AddListener(v => OnVolume(idx, v));
            if (mutes[i] != null) mutes[i].onValueChanged.AddListener(b => OnMute(idx, b));
        }
        wired = true;
    }

    private void Refresh()
    {
        var sound = Resolve<SoundMgr>();
        if (sound == null) return;
        refreshing = true;
        for (int i = 0; i < Channels.Length; i++)
        {
            float v = ChannelVolume(sound, i);
            if (sliders[i] != null) sliders[i].SetValueWithoutNotify(v);
            if (values[i] != null) values[i].text = Pct(v);
            if (mutes[i] != null) mutes[i].SetIsOnWithoutNotify(sound.IsChannelMuted(Channels[i]));
        }
        refreshing = false;
    }

    private void OnVolume(int idx, float v)
    {
        if (refreshing) return;
        var sound = Resolve<SoundMgr>();
        if (sound == null) return;
        switch (Channels[idx])
        {
            case SoundChannel.Master: sound.SetMasterVolume(v); break;
            case SoundChannel.Music: sound.SetBgmVolume(v); break;
            case SoundChannel.Sfx: sound.SetSfxVolume(v); break;
            case SoundChannel.Ui: sound.SetUiVolume(v); break;
        }
        if (values[idx] != null) values[idx].text = Pct(v);
    }

    private void OnMute(int idx, bool muted)
    {
        if (refreshing) return;
        var sound = Resolve<SoundMgr>();
        if (sound == null) return;
        switch (Channels[idx])
        {
            case SoundChannel.Master: sound.SetMasterMuted(muted); break;
            case SoundChannel.Music: sound.SetMusicMuted(muted); break;
            case SoundChannel.Sfx: sound.SetSfxMuted(muted); break;
            case SoundChannel.Ui: sound.SetUiMuted(muted); break;
        }
    }

    private static float ChannelVolume(SoundMgr sound, int idx)
    {
        switch (Channels[idx])
        {
            case SoundChannel.Master: return sound.MasterVolume;
            case SoundChannel.Music: return sound.MusicVolume;
            case SoundChannel.Sfx: return sound.SfxVolume;
            default: return sound.UiVolume;
        }
    }

    private static T Resolve<T>() where T : class
        => GameLoop.Instance != null && GameLoop.Instance.Ctx != null ? GameLoop.Instance.Ctx.Get<T>() : null;

    private static string Pct(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
}
