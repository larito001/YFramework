using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class SoundMgr : MonoBehaviour
{
    private const int MaxSfxEmitters = 16;

    private sealed class SoundEmitter
    {
        public AudioSource source;
        public ResourceHandle<AudioClip> clipHandle;
        public Coroutine releaseCoroutine;
        public string path;
        public SoundChannel channel;
        public float volumeScale = 1f;
        public float pitch = 1f;
        public int version;
    }

    private readonly List<SoundEmitter> sfxEmitters = new List<SoundEmitter>();
    private readonly SoundSettingsContainer settingsContainer = new SoundSettingsContainer();

    private SoundEmitter musicEmitter;
    private SoundSettingsData settings = new SoundSettingsData();
    private ResMgr resMgr;
    private StoreMgr storeMgr;
    private ICoroutineRunner coroutineRunner;
    private GameObject audioRoot;
    private Coroutine musicTransitionCoroutine;
    private int musicRequestVersion;

    public float MasterVolume => settings.masterVolume;
    public float MusicVolume => settings.musicVolume;
    public float SfxVolume => settings.sfxVolume;
    public float UiVolume => settings.uiVolume;

    public bool IsMusicPlaying => musicEmitter?.source != null && musicEmitter.source.isPlaying;
    public string CurrentMusicPath => musicEmitter?.path;

    public void PlayBGM(string path, float volume = 1f)
    {
        PlayMusic(path, volume);
    }

    public void StopBGM()
    {
        StopMusic();
    }

    public void PlaySFX(string path, float volume = 1f)
    {
        Play(path, SoundPlayOptions.Sfx(volume));
    }

    public void PlayUISFX(string path, float volume = 1f)
    {
        Play(path, SoundPlayOptions.Ui(volume));
    }

    public void Play(string path, SoundPlayOptions options)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[SoundMgr] Ignored play request because path is empty.");
            return;
        }

        if (options.Channel == SoundChannel.Music)
        {
            PlayMusic(path, options.VolumeScale, 0.15f, false, options.Loop);
            return;
        }

        resMgr.LoadHandleAsync<AudioClip>(path, handle =>
        {
            if (handle?.Asset == null)
            {
                return;
            }

            var emitter = GetFreeEmitter();
            BindClip(emitter, handle, path, options.Channel, options.VolumeScale, options.Pitch, options.Loop);
            emitter.source.loop = options.Loop;
            emitter.source.Play();

            if (!options.Loop)
            {
                StartReleaseWatcher(emitter);
            }
        });
    }

    public void PlayMusic(string path, float volumeScale = 1f, float fadeDuration = 0.15f,
        bool restartIfSame = false, bool loop = true)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[SoundMgr] Ignored music request because path is empty.");
            return;
        }

        if (!restartIfSame && musicEmitter?.source != null && musicEmitter.path == path && musicEmitter.clipHandle != null)
        {
            musicEmitter.volumeScale = Mathf.Clamp01(volumeScale);
            musicEmitter.source.loop = loop;
            ApplyEmitterVolume(musicEmitter);
            if (!musicEmitter.source.isPlaying)
            {
                musicEmitter.source.Play();
            }

            return;
        }

        musicRequestVersion++;
        var requestVersion = musicRequestVersion;
        resMgr.LoadHandleAsync<AudioClip>(path, handle =>
        {
            if (requestVersion != musicRequestVersion)
            {
                handle?.Release();
                return;
            }

            if (handle?.Asset == null)
            {
                return;
            }

            StopMusicTransition();
            musicTransitionCoroutine = coroutineRunner.Run(SwapMusic(handle, path, Mathf.Clamp01(volumeScale), fadeDuration, loop, requestVersion));
        });
    }

    public void StopMusic(float fadeDuration = 0.1f)
    {
        musicRequestVersion++;
        StopMusicTransition();

        if (musicEmitter?.source == null)
        {
            ReleaseEmitter(musicEmitter, true);
            return;
        }

        if (fadeDuration <= 0f || !musicEmitter.source.isPlaying)
        {
            ReleaseEmitter(musicEmitter, true);
            return;
        }

        musicTransitionCoroutine = coroutineRunner.Run(FadeOutAndReleaseMusic(fadeDuration));
    }

    public void StopAllSFX()
    {
        for (int i = 0; i < sfxEmitters.Count; i++)
        {
            ReleaseEmitter(sfxEmitters[i], true);
        }
    }

    public void StopAll()
    {
        StopMusic();
        StopAllSFX();
    }

    public float GetChannelVolume(SoundChannel channel)
    {
        return channel switch
        {
            SoundChannel.Master => settings.masterVolume,
            SoundChannel.Music => settings.musicVolume,
            SoundChannel.Sfx => settings.sfxVolume,
            SoundChannel.Ui => settings.uiVolume,
            _ => 1f,
        };
    }

    public bool IsChannelMuted(SoundChannel channel)
    {
        return channel switch
        {
            SoundChannel.Master => settings.masterMuted,
            SoundChannel.Music => settings.musicMuted,
            SoundChannel.Sfx => settings.sfxMuted,
            SoundChannel.Ui => settings.uiMuted,
            _ => false,
        };
    }

    public void SetMasterVolume(float volume)
    {
        SetChannelVolume(SoundChannel.Master, volume);
    }

    public void SetBgmVolume(float volume)
    {
        SetChannelVolume(SoundChannel.Music, volume);
    }

    public void SetSfxVolume(float volume)
    {
        SetChannelVolume(SoundChannel.Sfx, volume);
    }

    public void SetUiVolume(float volume)
    {
        SetChannelVolume(SoundChannel.Ui, volume);
    }

    public void SetMasterMuted(bool muted)
    {
        SetChannelMuted(SoundChannel.Master, muted);
    }

    public void SetMusicMuted(bool muted)
    {
        SetChannelMuted(SoundChannel.Music, muted);
    }

    public void SetSfxMuted(bool muted)
    {
        SetChannelMuted(SoundChannel.Sfx, muted);
    }

    public void SetUiMuted(bool muted)
    {
        SetChannelMuted(SoundChannel.Ui, muted);
    }

    public void SetChannelVolume(SoundChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);
        switch (channel)
        {
            case SoundChannel.Master:
                settings.masterVolume = volume;
                break;
            case SoundChannel.Music:
                settings.musicVolume = volume;
                break;
            case SoundChannel.Sfx:
                settings.sfxVolume = volume;
                break;
            case SoundChannel.Ui:
                settings.uiVolume = volume;
                break;
        }

        ApplySettings();
        SaveSettings();
    }

    public void SetChannelMuted(SoundChannel channel, bool muted)
    {
        switch (channel)
        {
            case SoundChannel.Master:
                settings.masterMuted = muted;
                break;
            case SoundChannel.Music:
                settings.musicMuted = muted;
                break;
            case SoundChannel.Sfx:
                settings.sfxMuted = muted;
                break;
            case SoundChannel.Ui:
                settings.uiMuted = muted;
                break;
        }

        ApplySettings();
        SaveSettings();
    }

    private void Awake()
    {
        var ctx = GameLoop.Instance.Ctx;
        resMgr = ctx.Get<ResMgr>();
        storeMgr = ctx.Get<StoreMgr>();
        coroutineRunner = ctx.Get<ICoroutineRunner>();

        settingsContainer.BindStore(storeMgr);

        audioRoot = new GameObject("SoundRoot");
        GameObject.DontDestroyOnLoad(audioRoot);

        musicEmitter = CreateEmitter("MusicSource", loop: true);
        for (int i = 0; i < 5; i++)
        {
            sfxEmitters.Add(CreateEmitter($"SfxSource_{i + 1}"));
        }

        settingsContainer.Load(() =>
        {
            settings = settingsContainer.GetData() ?? new SoundSettingsData();
            ApplySettings();
        });
    }

    private void OnDestroy()
    {
        StopMusicTransition();
        if (musicEmitter != null)
        {
            ReleaseEmitter(musicEmitter, true);
            musicEmitter = null;
        }

        for (int i = 0; i < sfxEmitters.Count; i++)
        {
            ReleaseEmitter(sfxEmitters[i], true);
        }

        sfxEmitters.Clear();
        resMgr = null;
        storeMgr = null;
        coroutineRunner = null;
        if (audioRoot != null)
        {
            GameObject.Destroy(audioRoot);
            audioRoot = null;
        }
    }

    private void ApplySettings()
    {
        ApplyEmitterVolume(musicEmitter);
        for (int i = 0; i < sfxEmitters.Count; i++)
        {
            ApplyEmitterVolume(sfxEmitters[i]);
        }
    }

    private void ApplyEmitterVolume(SoundEmitter emitter)
    {
        if (emitter?.source == null)
        {
            return;
        }

        emitter.source.volume = GetEffectiveVolume(emitter.channel, emitter.volumeScale);
    }

    private void BindClip(SoundEmitter emitter, ResourceHandle<AudioClip> handle, string path,
        SoundChannel channel, float volumeScale, float pitch, bool loop)
    {
        ReleaseEmitter(emitter, stopPlayback: true);

        emitter.clipHandle = handle;
        emitter.path = path;
        emitter.channel = channel;
        emitter.volumeScale = Mathf.Clamp01(volumeScale);
        emitter.pitch = pitch;
        emitter.version++;

        emitter.source.clip = handle.Asset;
        emitter.source.pitch = pitch;
        emitter.source.loop = loop;
        ApplyEmitterVolume(emitter);
    }

    private SoundEmitter CreateEmitter(string name, bool loop = false)
    {
        var emitterObject = new GameObject(name);
        emitterObject.transform.SetParent(audioRoot.transform, false);

        var source = emitterObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;

        return new SoundEmitter
        {
            source = source,
            channel = loop ? SoundChannel.Music : SoundChannel.Sfx
        };
    }

    private IEnumerator FadeOutAndReleaseMusic(float duration)
    {
        if (musicEmitter?.source == null)
        {
            yield break;
        }

        var source = musicEmitter.source;
        var startVolume = source.volume;
        var elapsed = 0f;
        while (elapsed < duration && source != null)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        ReleaseEmitter(musicEmitter, true);
        musicTransitionCoroutine = null;
    }

    private float GetEffectiveVolume(SoundChannel channel, float volumeScale)
    {
        if (settings.masterMuted || IsChannelMuted(channel))
        {
            return 0f;
        }

        return Mathf.Clamp01(settings.masterVolume * GetChannelVolume(channel) * volumeScale);
    }

    private SoundEmitter GetFreeEmitter()
    {
        for (int i = 0; i < sfxEmitters.Count; i++)
        {
            if (!sfxEmitters[i].source.isPlaying)
            {
                return sfxEmitters[i];
            }
        }

        if (sfxEmitters.Count >= MaxSfxEmitters)
        {
            var oldest = sfxEmitters[0];
            for (int i = 1; i < sfxEmitters.Count; i++)
            {
                if (sfxEmitters[i].version < oldest.version)
                {
                    oldest = sfxEmitters[i];
                }
            }

            ReleaseEmitter(oldest, true);
            return oldest;
        }

        var emitter = CreateEmitter($"SfxSource_{sfxEmitters.Count + 1}");
        sfxEmitters.Add(emitter);
        return emitter;
    }

    private void ReleaseEmitter(SoundEmitter emitter, bool stopPlayback)
    {
        if (emitter?.source == null)
        {
            return;
        }

        StopReleaseWatcher(emitter);

        if (stopPlayback && emitter.source.isPlaying)
        {
            emitter.source.Stop();
        }

        emitter.source.clip = null;
        emitter.source.loop = false;
        emitter.source.pitch = 1f;
        emitter.path = null;
        emitter.volumeScale = 1f;
        emitter.pitch = 1f;
        emitter.clipHandle?.Release();
        emitter.clipHandle = null;
    }

    private void SaveSettings()
    {
        settingsContainer.Save();
    }

    private IEnumerator SwapMusic(ResourceHandle<AudioClip> handle, string path, float volumeScale,
        float fadeDuration, bool loop, int requestVersion)
    {
        if (musicEmitter?.source == null)
        {
            handle.Release();
            yield break;
        }

        var source = musicEmitter.source;
        var hadMusic = source.isPlaying && musicEmitter.clipHandle != null;
        if (hadMusic && fadeDuration > 0f)
        {
            var startVolume = source.volume;
            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (requestVersion != musicRequestVersion)
                {
                    handle.Release();
                    musicTransitionCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
        }

        ReleaseEmitter(musicEmitter, true);
        BindClip(musicEmitter, handle, path, SoundChannel.Music, volumeScale, 1f, loop);

        if (fadeDuration > 0f)
        {
            source.volume = 0f;
        }

        source.Play();

        if (fadeDuration > 0f)
        {
            var targetVolume = GetEffectiveVolume(SoundChannel.Music, volumeScale);
            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (requestVersion != musicRequestVersion)
                {
                    musicTransitionCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            source.volume = targetVolume;
        }

        musicTransitionCoroutine = null;
    }

    private void StartReleaseWatcher(SoundEmitter emitter)
    {
        StopReleaseWatcher(emitter);
        var version = emitter.version;
        emitter.releaseCoroutine = coroutineRunner.Run(ReleaseWhenDone(emitter, version));
    }

    private void StopReleaseWatcher(SoundEmitter emitter)
    {
        if (emitter?.releaseCoroutine == null)
        {
            return;
        }

        coroutineRunner.Stop(emitter.releaseCoroutine);
        emitter.releaseCoroutine = null;
    }

    private void StopMusicTransition()
    {
        if (musicTransitionCoroutine == null)
        {
            return;
        }

        coroutineRunner.Stop(musicTransitionCoroutine);
        musicTransitionCoroutine = null;
    }

    private IEnumerator ReleaseWhenDone(SoundEmitter emitter, int version)
    {
        yield return new WaitWhile(() => emitter.source != null && emitter.source.isPlaying);

        if (emitter.source != null && emitter.version == version)
        {
            ReleaseEmitter(emitter, false);
        }

        emitter.releaseCoroutine = null;
    }
}
