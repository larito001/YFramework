using System;
using System.Collections.Generic;
using UnityEngine;

namespace YOTO
{
    /// <summary>
    /// 类型安全的全局事件分发器。事件键为任意 <see cref="Enum"/>，业务侧定义具体枚举（如
    /// <c>YOTOEventType</c> 在 GamePlay/Event/GameEventTypes.cs）。
    /// 同一个事件键一旦关联了某个委托签名，后续订阅必须保持一致。
    /// </summary>
    public class EventMgr : IGameService
    {
        private interface IEventSlot
        {
            bool IsEmpty { get; }
            Type CallbackType { get; }
            bool Contains(Delegate callback);
            void Add(Delegate callback);
            void Remove(Delegate callback);
        }

        private sealed class EventSlot<TDelegate> : IEventSlot where TDelegate : Delegate
        {
            private TDelegate callbacks;

            public bool IsEmpty => callbacks == null;
            public Type CallbackType => typeof(TDelegate);

            public bool Contains(Delegate callback)
            {
                if (callback is not TDelegate typedCallback || callbacks == null)
                {
                    return false;
                }

                return Array.IndexOf(callbacks.GetInvocationList(), typedCallback) >= 0;
            }

            public void Add(Delegate callback)
            {
                callbacks = (TDelegate)Delegate.Combine(callbacks, (TDelegate)callback);
            }

            public void Remove(Delegate callback)
            {
                callbacks = (TDelegate)Delegate.Remove(callbacks, (TDelegate)callback);
            }

            public TDelegate GetCallbacks()
            {
                return callbacks;
            }
        }

        private readonly Dictionary<Enum, IEventSlot> events = new();

        public void Add(Enum type, Action callback)
        {
            AddInternal(type, callback);
        }

        public void Add<T>(Enum type, Action<T> callback)
        {
            AddInternal(type, callback);
        }

        public void Add<T1, T2>(Enum type, Action<T1, T2> callback)
        {
            AddInternal(type, callback);
        }

        public void Add<T1, T2, T3>(Enum type, Action<T1, T2, T3> callback)
        {
            AddInternal(type, callback);
        }

        public void Add<T1, T2, T3, T4>(Enum type, Action<T1, T2, T3, T4> callback)
        {
            AddInternal(type, callback);
        }

        public void Remove(Enum type, Action callback)
        {
            RemoveInternal(type, callback);
        }

        public void Remove<T>(Enum type, Action<T> callback)
        {
            RemoveInternal(type, callback);
        }

        public void Remove<T1, T2>(Enum type, Action<T1, T2> callback)
        {
            RemoveInternal(type, callback);
        }

        public void Remove<T1, T2, T3>(Enum type, Action<T1, T2, T3> callback)
        {
            RemoveInternal(type, callback);
        }

        public void Remove<T1, T2, T3, T4>(Enum type, Action<T1, T2, T3, T4> callback)
        {
            RemoveInternal(type, callback);
        }

        public void Trigger(Enum type)
        {
            var slot = GetSlot<EventSlot<Action>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke();
        }

        public void Trigger<T>(Enum type, T arg)
        {
            var slot = GetSlot<EventSlot<Action<T>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg);
        }

        public void Trigger<T1, T2>(Enum type, T1 arg1, T2 arg2)
        {
            var slot = GetSlot<EventSlot<Action<T1, T2>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg1, arg2);
        }

        public void Trigger<T1, T2, T3>(Enum type, T1 arg1, T2 arg2, T3 arg3)
        {
            var slot = GetSlot<EventSlot<Action<T1, T2, T3>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg1, arg2, arg3);
        }

        public void Trigger<T1, T2, T3, T4>(Enum type, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            var slot = GetSlot<EventSlot<Action<T1, T2, T3, T4>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg1, arg2, arg3, arg4);
        }

        public void Clear()
        {
            events.Clear();
        }

        public void Init(GameContext ctx)
        {
            Clear();
        }

        public void Shutdown()
        {
            Clear();
        }

        private void AddInternal<TDelegate>(Enum type, TDelegate callback) where TDelegate : Delegate
        {
            if (callback == null)
            {
                return;
            }

            var slot = GetOrCreateSlot<EventSlot<TDelegate>>(type);
            if (slot == null)
            {
                return;
            }

            if (slot.Contains(callback))
            {
                return;
            }

            slot.Add(callback);
        }

        private void RemoveInternal<TDelegate>(Enum type, TDelegate callback) where TDelegate : Delegate
        {
            if (callback == null)
            {
                return;
            }

            var slot = GetSlot<EventSlot<TDelegate>>(type, shouldLogError: false);
            if (slot == null)
            {
                return;
            }

            slot.Remove(callback);
            if (slot.IsEmpty)
            {
                events.Remove(type);
            }
        }

        private TSlot GetOrCreateSlot<TSlot>(Enum type) where TSlot : class, IEventSlot, new()
        {
            if (events.TryGetValue(type, out var existingSlot))
            {
                if (existingSlot is TSlot typedSlot)
                {
                    return typedSlot;
                }

                Debug.LogError($"[EventMgr] Event {type} expected callback type {typeof(TSlot).Name}, but actual type is {existingSlot.CallbackType.Name}.");
                return null;
            }

            var newSlot = new TSlot();
            events[type] = newSlot;
            return newSlot;
        }

        private TSlot GetSlot<TSlot>(Enum type, bool shouldLogError) where TSlot : class, IEventSlot
        {
            if (!events.TryGetValue(type, out var slot))
            {
                return null;
            }

            if (slot is TSlot typedSlot)
            {
                return typedSlot;
            }

            if (shouldLogError)
            {
                Debug.LogError($"[EventMgr] Event {type} callback type mismatch. Actual type is {slot.CallbackType.Name}.");
            }

            return null;
        }
    }
}
