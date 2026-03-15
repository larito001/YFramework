using System;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

namespace HotUpdate.Scripts.Framework.Pool.newPool
{
    public class ObjectPool : IGameService
    {
        public class PoolBuffer
        {
            public class BufferItem
            {
                public Transform refTrans;
                public float lastActiveTime;

                public void Reset()
                {
                    if (refTrans != null)
                    {
                        GameObject.Destroy(refTrans.gameObject);
                        refTrans = null;
                    }

                    lastActiveTime = 0;
                }
            }

            public struct BufferConfig
            {
                public float loopCheckCDTime;
                public uint inactiveTimeMax;
                public uint perFrameDisposeCountMax;
                public uint poolSizeMax;
                public Vector3 recoverWorldPos;
                public bool isNotCheckRecover;
            }

            public string name;
            public Transform rootTrans;
            public BufferConfig config;

            private readonly ResMgr resMgr;
            private string resPath;
            private GameObject template;
            private bool isLoading;
            private event Action<GameObject, string, bool> getItemCompleteCallbacks;
            private event Action loadCompleteCallbacks;
            private Queue<BufferItem> items;
            private float lastLoopCheckTime;
            private uint usingCount;

            public PoolBuffer(ResMgr resourceManager)
            {
                resMgr = resourceManager;
            }

            public int BufferSize => items?.Count ?? 0;

            public void Init()
            {
                items = new Queue<BufferItem>();
                lastLoopCheckTime = 0;
                config = new BufferConfig
                {
                    loopCheckCDTime = 5,
                    inactiveTimeMax = 5,
                    perFrameDisposeCountMax = 10,
                    recoverWorldPos = new Vector3(1000, 1000, 1000),
                    isNotCheckRecover = false
                };
                isLoading = false;
            }

            public void SetupResPath(string path)
            {
                resPath = path;
            }

            public bool LoopCheck()
            {
                if (isLoading || items == null)
                {
                    return false;
                }

                if (Time.time - lastLoopCheckTime < config.loopCheckCDTime)
                {
                    return false;
                }

                lastLoopCheckTime = Time.time;

                var curFrameDisposeCount = 0;
                while (items.Count > 0 && curFrameDisposeCount <= config.perFrameDisposeCountMax)
                {
                    var curItem = items.Peek();
                    if ((Time.time - curItem.lastActiveTime) > config.inactiveTimeMax)
                    {
                        curFrameDisposeCount++;
                        items.Dequeue();
                        curItem.Reset();
                    }
                    else
                    {
                        break;
                    }
                }

                return !config.isNotCheckRecover && items.Count <= 0 && usingCount == 0;
            }

            public void AsyncLoadAndGetItem(Action<GameObject, string, bool> callback)
            {
                getItemCompleteCallbacks += callback;
                if (template == null)
                {
                    AsyncLoadItem(InvokeGetItemCompleteCallbacks);
                }
                else
                {
                    InvokeGetItemCompleteCallbacks();
                }
            }

            public void RemoveGetItemCompleteCallback(Action<GameObject, string, bool> callback)
            {
                getItemCompleteCallbacks -= callback;
            }

            public void RecoverItem(GameObject target)
            {
                if (target == null)
                {
                    return;
                }

                if (items.Count >= config.poolSizeMax)
                {
                    GameObject.Destroy(target);
                    ReduceUsingCount();
                    return;
                }

                target.transform.SetParent(rootTrans, false);
                target.transform.position = config.recoverWorldPos;
                if (template != null)
                {
                    target.transform.localScale = template.transform.localScale;
                }

                var bufferItem = new BufferItem
                {
                    refTrans = target.transform,
                    lastActiveTime = Time.time
                };
                items.Enqueue(bufferItem);
                ReduceUsingCount();
            }

            public void Reset()
            {
                name = string.Empty;

                if (template != null)
                {
                    GameObject.Destroy(template);
                    template = null;
                }

                if (items != null)
                {
                    while (items.Count > 0)
                    {
                        var curItem = items.Dequeue();
                        curItem.Reset();
                    }
                }

                if (rootTrans != null)
                {
                    GameObject.Destroy(rootTrans.gameObject);
                    rootTrans = null;
                    resPath = string.Empty;
                }

                getItemCompleteCallbacks = null;
                loadCompleteCallbacks = null;
                isLoading = false;
                config.isNotCheckRecover = false;
            }

            private void AsyncLoadItem(Action callback = null)
            {
                loadCompleteCallbacks += callback;
                if (template != null)
                {
                    InvokeLoadCompleteCallbacks();
                    return;
                }

                if (isLoading)
                {
                    return;
                }

                isLoading = true;
                resMgr.LoadGameObject(resPath, templateObj =>
                {
                    isLoading = false;
                    if (templateObj == null)
                    {
                        Debug.LogError("Missing resource: " + resPath);
                        return;
                    }

                    if (rootTrans == null)
                    {
                        GameObject.Destroy(templateObj);
                        return;
                    }

                    templateObj.SetActive(false);
                    template = templateObj;
                    InvokeLoadCompleteCallbacks();
                });
            }

            private void InvokeGetItemCompleteCallbacks()
            {
                if (getItemCompleteCallbacks == null)
                {
                    return;
                }

                var completeList = getItemCompleteCallbacks.GetInvocationList();
                for (int i = 0; i < completeList.Length; i++)
                {
                    var complete = completeList[i] as Action<GameObject, string, bool>;
                    var isNew = GetItem(out var resultObj);
                    if (resultObj != null)
                    {
                        complete?.Invoke(resultObj.gameObject, name, isNew);
                    }
                }

                getItemCompleteCallbacks = null;
            }

            private void InvokeLoadCompleteCallbacks()
            {
                if (loadCompleteCallbacks == null)
                {
                    return;
                }

                var completeList = loadCompleteCallbacks.GetInvocationList();
                for (int i = 0; i < completeList.Length; i++)
                {
                    (completeList[i] as Action)?.Invoke();
                }

                loadCompleteCallbacks = null;
            }

            private bool GetItem(out Transform target)
            {
                bool isNew = false;
                target = null;

                if (items.Count > 0)
                {
                    var curItem = items.Dequeue();
                    target = curItem.refTrans;
                    curItem.refTrans = null;
                    curItem.Reset();
                }
                else
                {
                    target = GameObject.Instantiate(template).transform;
                    target.SetParent(rootTrans, false);
                    isNew = true;
                }

                AddUsingCount();
                return isNew;
            }

            private void AddUsingCount()
            {
                usingCount++;
            }

            private void ReduceUsingCount()
            {
                if (usingCount > 0)
                {
                    usingCount--;
                }
            }
        }

        private List<PoolBuffer> buffers;
        private ResMgr resMgr;
        private Transform poolRoot;

        private void LoopCheck(object o)
        {
            if (buffers == null)
            {
                return;
            }

            for (int i = buffers.Count - 1; i >= 0; i--)
            {
                var curBuffer = buffers[i];
                var isEmpty = curBuffer.LoopCheck();
                if (!isEmpty)
                {
                    continue;
                }

                buffers[i].Reset();
                buffers.RemoveAt(i);
            }
        }

        public PoolBuffer GetBuffer(string bufferName, float loopCheckCDTime, uint inactiveTimeMax,
            uint perFrameDisposeCountMax, uint poolSizeMax)
        {
            for (int i = 0; i < buffers.Count; i++)
            {
                if (buffers[i].name == bufferName)
                {
                    return buffers[i];
                }
            }

            var target = new PoolBuffer(resMgr);
            target.Init();
            BufferInitSet(bufferName, target, loopCheckCDTime, inactiveTimeMax, perFrameDisposeCountMax, poolSizeMax);
            buffers.Add(target);
            return target;
        }

        public void Clear()
        {
            Timers.inst.Remove(LoopCheck);
            for (int i = buffers.Count - 1; i >= 0; i--)
            {
                buffers[i].Reset();
            }

            buffers.Clear();
            buffers = null;

            if (poolRoot != null)
            {
                GameObject.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }

        public void Init(GameContext ctx)
        {
            buffers = new List<PoolBuffer>();
            resMgr = ctx.Get<ResMgr>();

            poolRoot = new GameObject("ObjectPoolRoot").transform;
            GameObject.DontDestroyOnLoad(poolRoot.gameObject);

            ObjectBase.Configure(this);
            Timers.inst.Add(0.5f, -1, LoopCheck);
        }

        public void Shutdown()
        {
            Clear();
            ObjectBase.Configure(null);
            resMgr = null;
        }

        private void BufferInitSet(string bufferName, PoolBuffer target, float loopCheckCDTime, uint inactiveTimeMax,
            uint perFrameDisposeCountMax, uint poolSizeMax)
        {
            target.name = bufferName;
            target.rootTrans = new GameObject(bufferName).transform;
            target.rootTrans.SetParent(poolRoot, false);
            target.SetupResPath(bufferName);
            target.config.loopCheckCDTime = loopCheckCDTime;
            target.config.inactiveTimeMax = inactiveTimeMax;
            target.config.perFrameDisposeCountMax = perFrameDisposeCountMax;
            target.config.poolSizeMax = poolSizeMax;
        }
    }
}
