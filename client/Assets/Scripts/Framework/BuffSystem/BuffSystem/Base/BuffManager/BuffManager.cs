using UnityEngine;

namespace NoSLoofah.BuffSystem.Manager
{
    public class BuffManager : IBuffManager, IGameService
    {
        [HideInInspector]
        [SerializeField] private BuffCollection collection;
        private IBuffTagManager tagManager;

        public bool IsWorking => collection != null;
        public IBuffTagManager TagManager => tagManager;

        public void SetData(BuffCollection buffCollection)
        {
            collection = buffCollection;
        }

        public IBuff GetBuff(int id)
        {
            if (id < 0 || id >= collection.Size)
            {
                throw new System.Exception("Invalid Buff id " + id + " current size " + collection.Size);
            }

            if (collection.buffList[id] == null)
            {
                throw new System.Exception("Buff is null id " + id);
            }

            return collection.buffList[id].Clone();
        }

        public void RegisterBuffTagManager(IBuffTagManager mgr)
        {
            tagManager = mgr;
        }

        public void Init(GameContext ctx)
        {
            RegisterBuffTagManager(new BitBuffTagManager());
            BuffHandler.Configure(this);

            var runtimeConfig = ctx.Get<GameRuntimeConfig>();
            SetData(runtimeConfig.BuffCollection);
            tagManager.Init(runtimeConfig.BuffData);

            if (collection == null)
            {
                Debug.LogError("BuffCollection is null");
            }
        }

        public void Shutdown()
        {
            BuffHandler.Configure(null);
        }
    }
}
