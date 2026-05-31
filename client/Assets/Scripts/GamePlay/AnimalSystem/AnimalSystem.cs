using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 动物生成系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
    /// 读 animal 配表(目前 3 种,用箱子预制体占位),进对局时 <see cref="SpawnWave"/> 按权重随机选种、
    /// 在地面随机散布;每只挂 <see cref="AnimalEntity"/> 记配表 id 与击杀积分,供后续射击命中判定取用。
    /// </summary>
    public class AnimalSystem : IGameService
    {
        private const int DefaultCount = 8;     // 一波数量
        private const float Radius = 14f;       // 散布半径(米)
        private const float MinDistance = 4f;   // 离原点最小距离

        private ConfigManager config;
        private ResMgr res;
        private readonly List<Animal> catalog = new();
        private readonly List<GameObject> spawned = new();

        public void Init(GameContext ctx)
        {
            config = ctx.Get<ConfigManager>();
            res = ctx.Get<ResMgr>();
            BuildCatalog();
        }

        public void Shutdown()
        {
            Clear();
            catalog.Clear();
            config = null;
            res = null;
        }

        private void BuildCatalog()
        {
            catalog.Clear();
            var all = config?.animalConfig.items;
            if (all == null) return;
            foreach (var kv in all)
                if (kv.Value != null) catalog.Add(kv.Value);
        }

        /// <summary>清掉上一波,在地面随机生成 <paramref name="count"/> 只动物(按 weight 加权选种)。</summary>
        public void SpawnWave(int count = DefaultCount)
        {
            Clear();
            if (catalog.Count == 0) return;

            int totalWeight = 0;
            for (int i = 0; i < catalog.Count; i++) totalWeight += Mathf.Max(0, catalog[i].Weight);
            if (totalWeight <= 0) return;

            for (int i = 0; i < count; i++)
            {
                var animal = WeightedPick(totalWeight);
                if (animal == null) continue;

                var prefab = res.Load<GameObject>(animal.Prefab);
                if (prefab == null)
                {
                    Debug.LogWarning($"[AnimalSystem] 找不到动物预制体 {animal.Prefab}");
                    continue;
                }

                Vector3 pos = RandomGroundPos();
                var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                float s = animal.Scale > 0f ? animal.Scale : 1f;
                go.transform.localScale = Vector3.one * s;

                var entity = go.GetComponent<AnimalEntity>();
                if (entity == null) entity = go.AddComponent<AnimalEntity>();
                entity.animalId = (int)animal.Id;
                entity.score = animal.Score;

                spawned.Add(go);
            }
        }

        /// <summary>清除当前所有已生成的动物。</summary>
        public void Clear()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            spawned.Clear();
        }

        private Animal WeightedPick(int totalWeight)
        {
            int roll = Random.Range(0, totalWeight);
            int acc = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                acc += Mathf.Max(0, catalog[i].Weight);
                if (roll < acc) return catalog[i];
            }
            return catalog[catalog.Count - 1];
        }

        /// <summary>原点周围环形随机点;有地面碰撞体则射线贴地,否则 y=0。</summary>
        private Vector3 RandomGroundPos()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(MinDistance, Radius);
            Vector3 p = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 50f))
                p.y = hit.point.y;
            return p;
        }
    }
}
