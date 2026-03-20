using System;
using UnityEngine;
using YOTO;

public enum SoundChannel
{
    Master = 0,
    Music = 1,
    Sfx = 2,
    Ui = 3,
}

public readonly struct SoundPlayOptions
{
    public SoundPlayOptions(
        SoundChannel channel = SoundChannel.Sfx,
        float volumeScale = 1f,
        float pitch = 1f,
        bool loop = false)
    {
        Channel = channel;
        VolumeScale = volumeScale;
        Pitch = pitch;
        Loop = loop;
    }

    public SoundChannel Channel { get; }
    public float VolumeScale { get; }
    public float Pitch { get; }
    public bool Loop { get; }

    public static SoundPlayOptions Music(float volumeScale = 1f, bool loop = true)
    {
        return new SoundPlayOptions(SoundChannel.Music, volumeScale, 1f, loop);
    }

    public static SoundPlayOptions Sfx(float volumeScale = 1f, float pitch = 1f)
    {
        return new SoundPlayOptions(SoundChannel.Sfx, volumeScale, pitch, false);
    }

    public static SoundPlayOptions Ui(float volumeScale = 1f, float pitch = 1f)
    {
        return new SoundPlayOptions(SoundChannel.Ui, volumeScale, pitch, false);
    }
}

[Serializable]
public class SoundSettingsData
{
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 1f;

    public bool masterMuted;
    public bool musicMuted;
    public bool sfxMuted;
    public bool uiMuted;
}

public sealed class SoundSettingsContainer : DataContaner<SoundSettingsData>
{
    private SoundSettingsData data = new SoundSettingsData();

    public override string SaveKey => "sound_settings";

    public override SoundSettingsData GetData()
    {
        return data;
    }

    public override void __SetData(SoundSettingsData value)
    {
        data = value ?? new SoundSettingsData();
    }
}
