using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NoSLoofah.BuffSystem.Manager;

namespace NoSLoofah.BuffSystem
{
    public class BuffHandler : MonoBehaviour, IBuffHandler
    {
        private static IBuffManager sharedBuffManager;

        private readonly List<Buff> buffs = new List<Buff>();
        private Action onAddBuff;
        private Action onRemoveBuff;
        private bool updated;
        private Action forOnBuffDestroy;
        private Action forOnBuffStart;

        public static void Configure(IBuffManager buffManager)
        {
            sharedBuffManager = buffManager;
        }

        public List<Buff> GetBuffs => new List<Buff>(buffs);
        public void RegisterOnAddBuff(Action act) { onAddBuff += act; }
        public void RemoveOnAddBuff(Action act) { onRemoveBuff -= act; }
        public void RegisterOnRemoveBuff(Action act) { onRemoveBuff += act; }
        public void RemoveOnRemoveBuff(Action act) { onRemoveBuff -= act; }

        public void AddBuff(int buffId, GameObject caster)
        {
            var buff = sharedBuffManager.GetBuff(buffId);
            AddBuff(buff, caster);
        }

        public void RemoveBuff(int buffId, bool removeAll = true)
        {
            var buff = buffs.FirstOrDefault(b => b.ID == buffId);
            if (buff == null)
            {
                return;
            }

            if (buff.MutilAddType == BuffMutilAddType.multipleCount && removeAll)
            {
                var targets = buffs.Where(b => b.ID == buffId).ToList();
                foreach (var target in targets)
                {
                    RemoveBuff(target);
                }

                return;
            }

            RemoveBuff(buff);
        }

        public void InterruptBuff(int buffId, bool removeAll = true)
        {
            var buff = buffs.FirstOrDefault(b => b.ID == buffId);
            if (buff == null)
            {
                return;
            }

            if (buff.MutilAddType == BuffMutilAddType.multipleCount && removeAll)
            {
                var targets = buffs.Where(b => b.ID == buffId).ToList();
                foreach (var target in targets)
                {
                    InterruptBuff(target);
                }

                return;
            }

            InterruptBuff(buff);
        }

        private void Update()
        {
            if (updated) return;
            updated = true;
            forOnBuffDestroy?.Invoke();
            forOnBuffStart?.Invoke();
            forOnBuffDestroy = null;
            forOnBuffStart = null;
        }

        private void LateUpdate()
        {
            updated = false;
            bool buffRemoved = false;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var buff = buffs[i];
                buff.OnBuffUpdate();
                if (!buff.IsEffective)
                {
                    buff.OnBuffRemove();
                    buffRemoved = true;
                    buffs.Remove(buff);
                    forOnBuffDestroy += buff.OnBuffDestroy;
                }
            }

            if (buffRemoved)
            {
                onRemoveBuff?.Invoke();
            }
        }

        private void AddBuff(IBuff buff, GameObject caster)
        {
            if (!updated) Update();
            Buff newBuff = (Buff)buff;
            if (newBuff.IsEmpty())
            {
                Debug.LogError("Try add empty Buff");
                return;
            }

            newBuff.Initialize(this, caster);
            newBuff.OnBuffAwake();
            onAddBuff?.Invoke();

            Buff previous = buffs.Find(p => p.Equals(newBuff));
            if (previous == null)
            {
                if (newBuff.BuffTag != BuffTag.none)
                {
                    if (buffs.Any(b => sharedBuffManager.TagManager.IsTagCanAddWhenHaveOther(newBuff.BuffTag, b.BuffTag)))
                    {
                        newBuff.SetEffective(false);
                        newBuff.OnBuffDestroy();
                        return;
                    }

                    for (int i = buffs.Count - 1; i >= 0; i--)
                    {
                        if (sharedBuffManager.TagManager.IsTagRemoveOther(newBuff.BuffTag, buffs[i].BuffTag))
                        {
                            RemoveBuff(buffs[i]);
                        }
                    }
                }

                buffs.Add(newBuff);
                forOnBuffStart += newBuff.OnBuffStart;
                return;
            }

            switch (previous.MutilAddType)
            {
                case BuffMutilAddType.resetTime:
                    previous.ResetTimer();
                    break;
                case BuffMutilAddType.multipleLayer:
                    previous.ModifyLayer(1);
                    break;
                case BuffMutilAddType.multipleLayerAndResetTime:
                    previous.ResetTimer();
                    previous.ModifyLayer(1);
                    break;
                case BuffMutilAddType.multipleCount:
                    buffs.Add(newBuff);
                    forOnBuffStart += newBuff.OnBuffStart;
                    break;
            }
        }

        private void RemoveBuff(IBuff buff)
        {
            ((Buff)buff).SetEffective(false);
        }

        private void InterruptBuff(IBuff buff)
        {
            Buff target = (Buff)buff;
            target.SetEffective(false);
            buffs.Remove(target);
            forOnBuffDestroy += target.OnBuffDestroy;
        }
    }
}
