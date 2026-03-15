using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight runtime service container.
/// Owns service registration, initialization order, and frame dispatch.
/// </summary>
public sealed class GameContext
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<IGameService> _initOrder = new();

    private readonly List<ITickable> _tickables = new();
    private readonly List<IFixedTickable> _fixedTickables = new();
    private readonly List<ILateTickable> _lateTickables = new();

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

        if (instance is IGameService s) _initOrder.Add(s);
        if (instance is ITickable t) _tickables.Add(t);
        if (instance is IFixedTickable f) _fixedTickables.Add(f);
        if (instance is ILateTickable l) _lateTickables.Add(l);
    }

    /// <summary>
    /// Initializes all registered services.
    /// Keep this stage focused on dependency setup and subscriptions.
    /// Avoid spawning scene content here.
    /// </summary>
    public void InitAll()
    {
        for (int i = 0; i < _initOrder.Count; i++)
            _initOrder[i].Init(this);
    }

    public void ShutdownAll()
    {
        // Release in reverse init order to reduce dependency hazards.
        for (int i = _initOrder.Count - 1; i >= 0; i--)
            _initOrder[i].Shutdown();
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < _tickables.Count; i++)
            _tickables[i].Tick(dt);
    }

    public void FixedTick(float fdt)
    {
        for (int i = 0; i < _fixedTickables.Count; i++)
            _fixedTickables[i].FixedTick(fdt);
    }

    public void LateTick(float dt)
    {
        for (int i = 0; i < _lateTickables.Count; i++)
            _lateTickables[i].LateTick(dt);
    }
}
