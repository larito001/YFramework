using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 动物生成系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
    /// 读 animal 配表(prefab 指向 Resources/Animals 下的低多边形动物,只播 idle),
    /// 进对局时 <see cref="SpawnWave"/> 按权重随机选种、在地面随机散布;每只挂 <see cref="AnimalEntity"/>
    /// 记配表 id 与击杀积分,供后续射击命中判定取用。预制体由 AnimalPrefabBuilder 菜单生成。
    /// **刷怪范围按关卡区分**:每波只在「当前选中关卡(<see cref="MapSystem"/>)的动物池」(map 配表 animals 列)里选种,
    /// 所以不同地图刷出的动物不同;关卡没配动物池时回退到全部动物。
    /// </summary>
    public class AnimalSystem : IGameService
    {
        private const int DefaultCount = 8;     // 一波数量
        private const float Radius = 14f;       // 散布半径(米)
        private const float MinDistance = 4f;   // 离原点最小距离
        private const float GoldenChance = 0.5f; // 刷出金色泛光稀有体的概率(1/2)
        private const string GoldShaderPath = "Shaders/AnimalGold"; // Resources 下的金色泛光 shader

        private GameContext ctx;
        private ConfigManager config;
        private ResMgr res;
        private readonly List<Animal> catalog = new();
        private readonly List<GameObject> spawned = new();
        private Material goldMaterial; // 金色泛光材质(全部金色动物共用,首次用时按 shader 创建)

        public void Init(GameContext ctx)
        {
            this.ctx = ctx;
            config = ctx.Get<ConfigManager>();
            res = ctx.Get<ResMgr>();
        }

        public void Shutdown()
        {
            Clear();
            catalog.Clear();
            if (goldMaterial != null) { Object.Destroy(goldMaterial); goldMaterial = null; }
            ctx = null;
            config = null;
            res = null;
        }

        /// <summary>按当前选中关卡的动物池构建本波候选(map 配表 animals 列);关卡没配则回退全部动物。</summary>
        private void BuildCatalog()
        {
            catalog.Clear();
            if (config == null) return;

            // 1) 选中关卡的动物池:逐 id 从 animal 配表取
            var maps = ctx?.Get<MapSystem>();
            var map = maps != null ? maps.Get(maps.SelectedMapId) : null;
            if (map != null && map.Animals.Count > 0)
            {
                foreach (var id in map.Animals)
                {
                    var a = config.animalConfig.Get(id);
                    if (a != null) catalog.Add(a);
                }
            }

            // 2) 回退:关卡没配动物池(或都取不到)时,用全部动物,保证总能刷出东西
            if (catalog.Count == 0)
            {
                var all = config.animalConfig.items;
                if (all == null) return;
                foreach (var kv in all)
                    if (kv.Value != null) catalog.Add(kv.Value);
            }
        }

        /// <summary>清掉上一波,在地面随机生成 <paramref name="count"/> 只动物(按 weight 加权选种)。</summary>
        public void SpawnWave(int count = DefaultCount)
        {
            Clear();
            BuildCatalog(); // 每波按当前选中关卡的动物池重建候选
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
                // 跳过弱点高亮盒(挂在 AnimalHitZone 下),否则会被染成金色丢掉红/黄标注
                if (renderers[i].GetComponentInParent<AnimalHitZone>() != null) continue;
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
