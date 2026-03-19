using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class SoundMgr : IGameService
{
    private AudioSource _bgmSource;
    private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, ResourceHandle<AudioClip>> _sfxHandles = new Dictionary<AudioSource, ResourceHandle<AudioClip>>();
    private readonly Dictionary<AudioSource, int> _sfxVersions = new Dictionary<AudioSource, int>();
    private ResourceHandle<AudioClip> _currentBgmHandle;
    private ResMgr _resMgr;
    private ICoroutineRunner _coroutineRunner;
    private GameObject _audioRoot;
    private int _bgmRequestVersion;

    public void PlayBGM(string path, float volume = 1f)
    {
        _bgmRequestVersion++;
        var requestVersion = _bgmRequestVersion;
        _resMgr.LoadHandleAsync<AudioClip>(path, handle =>
        {
            if (requestVersion != _bgmRequestVersion)
            {
                handle?.Release();
                return;
            }

            if (handle?.Asset == null)
            {
                return;
            }

            ReleaseCurrentBgm();
            _currentBgmHandle = handle;
            _bgmSource.clip = handle.Asset;
            _bgmSource.volume = volume;
            _bgmSource.Play();
        });
    }

    public void StopBGM()
    {
        _bgmRequestVersion++;
        _bgmSource?.Stop();
        ReleaseCurrentBgm();
    }

    public void PlaySFX(string path, float volume = 1f)
    {
        _resMgr.LoadHandleAsync<AudioClip>(path, handle =>
        {
            if (handle?.Asset == null)
            {
                return;
            }

            AudioSource src = GetFreeSfxSource();
            ReleaseSourceHandle(src);

            var version = _sfxVersions.TryGetValue(src, out var currentVersion) ? currentVersion + 1 : 1;
            _sfxVersions[src] = version;
            _sfxHandles[src] = handle;

            src.clip = handle.Asset;
            src.volume = volume;
            src.Play();
            _coroutineRunner.Run(ReleaseWhenDone(src, version));
        });
    }

    private IEnumerator ReleaseWhenDone(AudioSource src, int version)
    {
        yield return new WaitWhile(() => src.isPlaying);

        if (_sfxVersions.TryGetValue(src, out var currentVersion) && currentVersion == version)
        {
            src.clip = null;
            ReleaseSourceHandle(src);
        }
    }

    public void StopAllSFX()
    {
        foreach (var s in _sfxSources)
        {
            if (s.isPlaying)
            {
                s.Stop();
            }

            s.clip = null;
            ReleaseSourceHandle(s);
        }
    }

    public void SetBgmVolume(float volume)
    {
        if (_bgmSource != null) _bgmSource.volume = volume;
    }

    public void SetSfxVolume(float volume)
    {
        foreach (var s in _sfxSources)
        {
            s.volume = volume;
        }
    }

    private AudioSource GetFreeSfxSource()
    {
        foreach (var s in _sfxSources)
        {
            if (!s.isPlaying) return s;
        }

        var newSfx = _audioRoot.AddComponent<AudioSource>();
        newSfx.playOnAwake = false;
        _sfxSources.Add(newSfx);
        return newSfx;
    }

    public void Init(GameContext ctx)
    {
        _resMgr = ctx.Get<ResMgr>();
        _coroutineRunner = ctx.Get<ICoroutineRunner>();

        _audioRoot = new GameObject("SoundRoot");
        GameObject.DontDestroyOnLoad(_audioRoot);

        _bgmSource = _audioRoot.AddComponent<AudioSource>();
        _bgmSource.loop = true;

        for (int i = 0; i < 5; i++)
        {
            var sfx = _audioRoot.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            _sfxSources.Add(sfx);
        }
    }

    public void Shutdown()
    {
        StopBGM();
        StopAllSFX();
        _resMgr = null;
        _coroutineRunner = null;
        if (_audioRoot != null)
        {
            GameObject.Destroy(_audioRoot);
            _audioRoot = null;
        }
    }

    private void ReleaseCurrentBgm()
    {
        _currentBgmHandle?.Release();
        _currentBgmHandle = null;
        if (_bgmSource != null)
        {
            _bgmSource.clip = null;
        }
    }

    private void ReleaseSourceHandle(AudioSource source)
    {
        if (_sfxHandles.TryGetValue(source, out var handle))
        {
            handle.Release();
            _sfxHandles.Remove(source);
        }
    }
}
