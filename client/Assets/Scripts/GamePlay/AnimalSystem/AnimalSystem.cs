using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 动物生成系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
    /// 读 animal 配表(目前 3 种,prefab 指向 Resources/Animals 下的低多边形动物,只播 idle),
    /// 进对局时 <see cref="SpawnWave"/> 按权重随机选种、在地面随机散布;每只挂 <see cref="AnimalEntity"/>
    /// 记配表 id 与击杀积分,供后续射击命中判定取用。预制体由 AnimalPrefabBuilder 菜单生成。
    /// </summary>
    public class AnimalSystem : IGameService
    {
        private const int DefaultCount = 8;     // 一波数量
        private const float Radius = 14f;       // 散布半径(米)
        private const float MinDistance = 4f;   // 离原点最小距离
        private const float GoldenChance = 0.5f; // 刷出金色泛光稀有体的概率(1/2)
        private const string GoldShaderPath = "Shaders/AnimalGold"; // Resources 下的金色泛光 shader

        private ConfigManager config;
        private ResMgr res;
        private readonly List<Animal> catalog = new();
        private readonly List<GameObject> spawned = new();
        private Material goldMaterial; // 金色泛光材质(全部金色动物共用,首次用时按 shader 创建)

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
            if (goldMaterial != null) { Object.Destroy(goldMaterial); goldMaterial = null; }
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

                // 1/2 概率刷成金色泛光稀有体:把全身材质换成金色 shader(纯表现)。
                entity.isGolden = Random.value < GoldenChance;
                if (entity.isGolden) ApplyGold(go);

                // 挂随机游走:在出生点附近 idle/行走来回走动(行走动画参数自动探测),被击杀后自动停下。
                if (go.GetComponent<AnimalWander>() == null) go.AddComponent<AnimalWander>();

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

        /// <summary>把整只动物(所有子 Renderer 的所有材质槽)换成金色泛光材质;材质共用,失败则静默跳过(仍照常刷怪)。</summary>
        private void ApplyGold(GameObject go)
        {
            var mat = GetGoldMaterial();
            if (mat == null) return;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var slots = renderers[i].sharedMaterials;
                for (int s = 0; s < slots.Length; s++) slots[s] = mat;
                renderers[i].sharedMaterials = slots;
            }
        }

        /// <summary>懒加载金色泛光材质:按 Resources 下的 AnimalGold shader 建一份共用材质;找不到 shader 则告警返回 null。</summary>
        private Material GetGoldMaterial()
        {
            if (goldMaterial != null) return goldMaterial;

            var shader = res != null ? res.Load<Shader>(GoldShaderPath) : null;
            if (shader == null) shader = Shader.Find("Custom/AnimalGold");
            if (shader == null)
            {
                Debug.LogWarning($"[AnimalSystem] 找不到金色泛光 shader(Resources/{GoldShaderPath} 或 Custom/AnimalGold),金色动物退化为原色。");
                return null;
            }

            goldMaterial = new Material(shader) { name = "AnimalGold (runtime)" };
            return goldMaterial;
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
