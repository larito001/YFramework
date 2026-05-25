using System;
using System.Collections.Generic;

/// <summary>
/// 服务定位器（Service Locator）。
/// 各服务的生命周期和帧驱动交给 MonoBehaviour 自己处理（Awake / Update / OnDestroy），
/// GameContext 只负责注册与查找。
/// </summary>
public sealed class GameContext
{
    private readonly Dictionary<Type, object> _services = new();

    public T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj))
            return (T)obj;
        throw new InvalidOperationException($"Service not found: {typeof(T).Name}");
    }

    public bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = (T)obj;
            return true;
        }
        service = null;
        return false;
    }

    public void Register<T>(T instance) where T : class
    {
        var type = typeof(T);
        if (_services.ContainsKey(type))
            throw new InvalidOperationException($"Duplicate service: {type.Name}");
        _services[type] = instance;
    }
}
