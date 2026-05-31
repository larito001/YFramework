using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>声音设置页签:主/音乐/音效/界面 四路音量滑条 + 静音开关,绑定 SoundMgr(改动即生效并存盘)。</summary>
public class SoundSettingsTab : SettingTabBase
{
    private static readonly SoundChannel[] Channels = { SoundChannel.Master, SoundChannel.Music, SoundChannel.Sfx, SoundChannel.Ui };
    private static readonly string[] Names = { "主音量", "音乐", "音效", "界面" };

    private readonly Slider[] sliders = new Slider[4];
    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[4];
    private readonly Toggle[] mutes = new Toggle[4];
    private bool refreshing;

    protected override void Build()
    {
        NewLabel(Content, "声音设置", 400, 30, TextAlignmentOptions.Left);
        for (int i = 0; i < Channels.Length; i++)
        {
            int idx = i;
            var row = NewRow(Content);
            NewLabel(row.transform, Names[i], 120, 24, TextAlignmentOptions.Left);
            sliders[i] = NewSlider(row.transform);
            values[i] = NewLabel(row.transform, "100%", 70, 22, TextAlignmentOptions.Right);
            NewLabel(row.transform, "静音", 60, 20, TextAlignmentOptions.Right);
            mutes[i] = NewToggle(row.transform);

            sliders[i].onValueChanged.AddListener(v => OnVolume(idx, v));
            mutes[i].onValueChanged.AddListener(b => OnMute(idx, b));
        }
    }

    protected override void Refresh()
    {
        var sound = Resolve<SoundMgr>();
        if (sound == null) return;
        refreshing = true;
        for (int i = 0; i < Channels.Length; i++)
        {
            float v = ChannelVolume(sound, i);
            sliders[i].SetValueWithoutNotify(v);
            values[i].text = Pct(v);
            mutes[i].SetIsOnWithoutNotify(sound.IsChannelMuted(Channels[i]));
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
        values[idx].text = Pct(v);
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

    private static string Pct(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
}
