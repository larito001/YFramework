using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class SoundMgr : IGameService
{
    private AudioSource _bgmSource;
    private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
    private AudioClip _currentBgm;
    private ResMgr _resMgr;
    private ICoroutineRunner _coroutineRunner;
    private GameObject _audioRoot;

    public void PlayBGM(string path, float volume = 1f)
    {
        _resMgr.LoadAudio(path, clip =>
        {
            if (clip == null) return;

            _currentBgm = clip;
            _bgmSource.clip = clip;
            _bgmSource.volume = volume;
            _bgmSource.Play();
        });
    }

    public void StopBGM()
    {
        _bgmSource?.Stop();

        if (_currentBgm != null)
        {
            _resMgr.ReleasePack("Sound/BGM1", _currentBgm);
            _currentBgm = null;
            _bgmSource.clip = null;
        }
    }

    public void PlaySFX(string path, float volume = 1f)
    {
        _resMgr.LoadAudio(path, clip =>
        {
            if (clip == null) return;

            AudioSource src = GetFreeSfxSource();
            src.clip = clip;
            src.volume = volume;
            src.Play();
            _coroutineRunner.Run(ReleaseWhenDone(src, clip, path));
        });
    }

    private IEnumerator ReleaseWhenDone(AudioSource src, AudioClip clip, string path)
    {
        yield return new WaitWhile(() => src.isPlaying);
        src.clip = null;
        _resMgr.ReleasePack(path, clip);
    }

    public void StopAllSFX()
    {
        foreach (var s in _sfxSources)
        {
            if (s.isPlaying) s.Stop();
            if (s.clip != null)
            {
                _resMgr.ReleasePack("Sound/BGM1", s.clip);
                s.clip = null;
            }
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
}
