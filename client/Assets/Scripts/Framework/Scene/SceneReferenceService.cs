using System.Collections.Generic;
using UnityEngine;

public class SceneReferenceService : IGameService
{
    private readonly Dictionary<string, Transform> _explicitTransforms = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Transform> _transformCache = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Light> _lightCache = new Dictionary<string, Light>();
    private bool _explicitReferencesLoaded;

    public bool TryGetTransform(string objectName, out Transform target)
    {
        if (TryGetCached(_transformCache, objectName, out target))
        {
            return true;
        }

        LoadExplicitReferencesIfNeeded();
        if (TryGetCached(_explicitTransforms, objectName, out target))
        {
            _transformCache[objectName] = target;
            return true;
        }

        var found = GameObject.Find(objectName);
        if (found == null)
        {
            target = null;
            return false;
        }

        target = found.transform;
        _transformCache[objectName] = target;
        return true;
    }

    public bool TryGetLight(string objectName, out Light target)
    {
        if (TryGetCached(_lightCache, objectName, out target))
        {
            return true;
        }

        if (!TryGetTransform(objectName, out var targetTransform))
        {
            target = null;
            return false;
        }

        target = targetTransform.GetComponent<Light>();
        if (target == null)
        {
            return false;
        }

        _lightCache[objectName] = target;
        return true;
    }

    public void InvalidateCache()
    {
        _explicitTransforms.Clear();
        _transformCache.Clear();
        _lightCache.Clear();
        _explicitReferencesLoaded = false;
    }

    public void Init(GameContext ctx)
    {
        InvalidateCache();
    }

    public void Shutdown()
    {
        InvalidateCache();
    }

    private void LoadExplicitReferencesIfNeeded()
    {
        if (_explicitReferencesLoaded)
        {
            return;
        }

        _explicitReferencesLoaded = true;
        var providers = Object.FindObjectsOfType<SceneReferenceProvider>(true);
        for (int i = 0; i < providers.Length; i++)
        {
            var entries = providers[i].References;
            for (int j = 0; j < entries.Count; j++)
            {
                var entry = entries[j];
                if (string.IsNullOrWhiteSpace(entry.key) || entry.target == null)
                {
                    continue;
                }

                _explicitTransforms[entry.key] = entry.target;
            }
        }
    }

    private static bool TryGetCached<T>(Dictionary<string, T> cache, string key, out T value) where T : Object
    {
        if (cache.TryGetValue(key, out value) && value != null)
        {
            return true;
        }

        value = null;
        return false;
    }
}
