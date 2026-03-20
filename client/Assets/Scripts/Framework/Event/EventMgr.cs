using System;
using System.Collections.Generic;
using UnityEngine;

namespace YOTO
{
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

        private readonly Dictionary<YOTOEventType, IEventSlot> events = new();

        public void Add(YOTOEventType type, Action callback)
        {
            Add(type, callback);
        }

        public void Add<T>(YOTOEventType type, Action<T> callback)
        {
            Add(type, callback);
        }

        public void Add<T1, T2>(YOTOEventType type, Action<T1, T2> callback)
        {
            Add(type, callback);
        }

        public void Add<T1, T2, T3>(YOTOEventType type, Action<T1, T2, T3> callback)
        {
            Add(type, callback);
        }

        public void Add<T1, T2, T3, T4>(YOTOEventType type, Action<T1, T2, T3, T4> callback)
        {
            Add(type, callback);
        }

        public void Remove(YOTOEventType type, Action callback)
        {
            Remove(type, callback);
        }

        public void Remove<T>(YOTOEventType type, Action<T> callback)
        {
            Remove(type, callback);
        }

        public void Remove<T1, T2>(YOTOEventType type, Action<T1, T2> callback)
        {
            Remove(type, callback);
        }

        public void Remove<T1, T2, T3>(YOTOEventType type, Action<T1, T2, T3> callback)
        {
            Remove(type, callback);
        }

        public void Remove<T1, T2, T3, T4>(YOTOEventType type, Action<T1, T2, T3, T4> callback)
        {
            Remove(type, callback);
        }

        public void Trigger(YOTOEventType type)
        {
            var slot = GetSlot<EventSlot<Action>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke();
        }

        public void Trigger<T>(YOTOEventType type, T arg)
        {
            var slot = GetSlot<EventSlot<Action<T>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg);
        }

        public void Trigger<T1, T2>(YOTOEventType type, T1 arg1, T2 arg2)
        {
            var slot = GetSlot<EventSlot<Action<T1, T2>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg1, arg2);
        }

        public void Trigger<T1, T2, T3>(YOTOEventType type, T1 arg1, T2 arg2, T3 arg3)
        {
            var slot = GetSlot<EventSlot<Action<T1, T2, T3>>>(type, shouldLogError: true);
            slot?.GetCallbacks()?.Invoke(arg1, arg2, arg3);
        }

        public void Trigger<T1, T2, T3, T4>(YOTOEventType type, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
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

        private void Add<TDelegate>(YOTOEventType type, TDelegate callback) where TDelegate : Delegate
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

        private void Remove<TDelegate>(YOTOEventType type, TDelegate callback) where TDelegate : Delegate
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

        private TSlot GetOrCreateSlot<TSlot>(YOTOEventType type) where TSlot : class, IEventSlot, new()
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

        private TSlot GetSlot<TSlot>(YOTOEventType type, bool shouldLogError) where TSlot : class, IEventSlot
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
