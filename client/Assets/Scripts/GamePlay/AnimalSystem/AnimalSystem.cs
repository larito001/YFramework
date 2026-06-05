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
        private int spawnVersion;      // 每次 SpawnWave/SpawnCorpses/Clear 自增:异步加载回调比对,过期则丢弃(防快速重刷覆盖)

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

            int v = ++spawnVersion;
            // 先确保金色材质就绪(shader 异步加载),再逐只异步加载预制体生成,避免金色体刷出时材质还没好。
            EnsureGoldMaterial(() =>
            {
                if (v != spawnVersion) return; // 期间又重刷/清场:放弃本波
                for (int i = 0; i < count; i++)
                {
                    var animal = WeightedPick(totalWeight);
                    if (animal != null) SpawnOne(animal, v);
                }
            });
        }

        /// <summary>异步加载并生成一只动物(回调里比对 <see cref="spawnVersion"/>,过期则丢弃)。</summary>
        private void SpawnOne(Animal animal, int v)
        {
            res.LoadAsync<GameObject>(animal.Prefab, prefab =>
            {
                if (v != spawnVersion) { if (prefab != null) res.Release<GameObject>(animal.Prefab); return; }
                if (prefab == null)
                {
                    Debug.LogWarning($"[AnimalSystem] 找不到动物预制体 {animal.Prefab}");
                    return;
                }

                Vector3 pos = RandomGroundPos();
                var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                res.Release<GameObject>(animal.Prefab); // 实例已建,释放 prefab 引用(配平 LoadAsync 的 +1)
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
            });
        }

        /// <summary>
        /// 惊扰:让以 <paramref name="center"/> 为圆心、<paramref name="radius"/>(枪的 disturbRange)半径内的所有活体动物
        /// 进入 <paramref name="duration"/> 秒惊慌狂奔;范围外不受影响。每次开枪由 <see cref="GameMainPanel"/> 以子弹落点调用。
        /// </summary>
        public void PanicAround(Vector3 center, float radius, float duration)
        {
            if (radius <= 0f) return;
            float sqr = radius * radius;
            for (int i = 0; i < spawned.Count; i++)
            {
                var go = spawned[i];
                if (go == null) continue;
                var entity = go.GetComponent<AnimalEntity>();
                if (entity == null || entity.IsDead) continue;
                Vector3 d = go.transform.position - center; d.y = 0f; // 只看水平距离
                if (d.sqrMagnitude > sqr) continue;
                go.GetComponent<AnimalWander>()?.Panic(duration);
            }
        }

        /// <summary>开镜/退镜:显隐所有活体动物的头/心脏弱点高亮(身体永不显示)。由 <see cref="GameMainPanel"/> 在瞄准切换时调用。</summary>
        public void SetWeakPointHighlights(bool show)
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                var go = spawned[i];
                if (go == null) continue;
                var entity = go.GetComponent<AnimalEntity>();
                if (entity != null && entity.IsDead) continue; // 死亡个体不亮
                foreach (var hz in go.GetComponentsInChildren<AnimalHitZone>(true))
                    hz.SetHighlight(show);
            }
        }

        /// <summary>清除当前所有已生成的动物。</summary>
        public void Clear()
        {
            spawnVersion++; // 取消在途的异步生成,避免回调把动物又建出来
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            spawned.Clear();
        }

        /// <summary>
        /// 结束打猎时:清掉场上所有活体,按本局击杀清单(animalId→数量)在地面上摆成尸体网格(死亡姿势、无碰撞、不游走、不高亮)。
        /// 预制体异步加载,全部就绪后通过 <paramref name="onComplete"/> 回传所有尸体的包围盒,供相机抬起检视时取景;
        /// 没有击杀则回原点附近的空盒。尸体也记入 <see cref="spawned"/>,离开对局时随 <see cref="Clear"/> 一并清掉。
        /// </summary>
        public void SpawnCorpses(Dictionary<int, int> kills, System.Action<Bounds> onComplete)
        {
            Clear(); // 先移除场上活物

            // 把击杀清单展开成逐只列表
            var ids = new List<int>();
            if (kills != null)
                foreach (var kv in kills)
                    for (int n = 0; n < kv.Value; n++) ids.Add(kv.Key);

            if (ids.Count == 0 || config == null || res == null)
            {
                onComplete?.Invoke(new Bounds(GroundAt(Vector3.zero), Vector3.one));
                return;
            }

            // 竖屏:窄列、沿纵深(Z)排开,匹配竖屏的"高"画面;≤4 只单列,更多两列
            int cols = ids.Count <= 4 ? 1 : 2;
            int rows = Mathf.CeilToInt(ids.Count / (float)cols);
            const float spacing = 2.4f;                 // 尸体间距(米),靠紧一点便于镜头拉近
            float halfX = (cols - 1) * 0.5f * spacing;
            float halfZ = (rows - 1) * 0.5f * spacing;

            int v = ++spawnVersion;
            Bounds bounds = default;
            bool boundsInit = false;
            int pending = ids.Count;
            bool reported = false;

            void Finish()
            {
                if (reported) return;
                reported = true;
                if (!boundsInit) { onComplete?.Invoke(new Bounds(GroundAt(Vector3.zero), Vector3.one)); return; }
                var b = bounds;
                b.Expand(spacing); // 四周留点余量,取景不至于贴边
                onComplete?.Invoke(b);
            }

            for (int i = 0; i < ids.Count; i++)
            {
                int idx = i;
                var def = config.animalConfig.Get((uint)ids[i]);
                if (def == null) { if (--pending == 0) Finish(); continue; }
                res.LoadAsync<GameObject>(def.Prefab, prefab =>
                {
                    if (v != spawnVersion)
                    {
                        if (prefab != null) res.Release<GameObject>(def.Prefab);
                        if (--pending == 0) Finish();
                        return;
                    }
                    if (prefab != null)
                    {
                        int row = idx / cols, col = idx % cols;
                        Vector3 pos = GroundAt(new Vector3(col * spacing - halfX, 0f, row * spacing - halfZ));

                        var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                        res.Release<GameObject>(def.Prefab); // 配平 LoadAsync 的 +1
                        float s = def.Scale > 0f ? def.Scale : 1f;
                        go.transform.localScale = Vector3.one * s;
                        MakeCorpse(go);
                        spawned.Add(go);

                        if (!boundsInit) { bounds = new Bounds(pos, Vector3.zero); boundsInit = true; }
                        else bounds.Encapsulate(pos);
                    }
                    if (--pending == 0) Finish();
                });
            }
        }

        /// <summary>把一只刚实例化的动物变成尸体:摆死亡姿势、关碰撞/弱点高亮、去掉游走,不再可命中。</summary>
        private void MakeCorpse(GameObject go)
        {
            var wander = go.GetComponent<AnimalWander>();
            if (wander != null) Object.Destroy(wander); // 尸体不动

            // 关掉所有部位碰撞体 + 弱点高亮盒(都挂在 AnimalHitZone 节点下)
            foreach (var hz in go.GetComponentsInChildren<AnimalHitZone>(true))
                hz.gameObject.SetActive(false);

            // 摆死亡姿势(用 prefab 上烘焙好的死亡参数名)
            var entity = go.GetComponent<AnimalEntity>();
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && entity != null && !string.IsNullOrEmpty(entity.deathBool))
                animator.SetBool(entity.deathBool, true);
        }

        /// <summary>把 x/z 点贴到地面(有地面碰撞体则射线落点,否则 y=0)。</summary>
        private Vector3 GroundAt(Vector3 xz)
        {
            Vector3 p = new Vector3(xz.x, 0f, xz.z);
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 50f))
                p.y = hit.point.y;
            return p;
        }

        /// <summary>把整只动物(所有子 Renderer 的所有材质槽)换成金色泛光材质;材质共用,失败则静默跳过(仍照常刷怪)。</summary>
        private void ApplyGold(GameObject go)
        {
            if (goldMaterial == null) return; // 由 EnsureGoldMaterial 在生成前异步备好

            var mat = goldMaterial;
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

        /// <summary>确保金色泛光材质就绪后回调 <paramref name="done"/>:已建好直接回调;否则异步加载 Resources 下的 AnimalGold shader
        /// 建一份共用材质(找不到 shader 则退 Shader.Find,再不行告警、金色动物退原色)。shader 进程级常驻不释放。</summary>
        private void EnsureGoldMaterial(System.Action done)
        {
            if (goldMaterial != null) { done?.Invoke(); return; }

            if (res == null)
            {
                var fb = Shader.Find("Custom/AnimalGold");
                if (fb != null) goldMaterial = new Material(fb) { name = "AnimalGold (runtime)" };
                else Debug.LogWarning($"[AnimalSystem] 找不到金色泛光 shader(Custom/AnimalGold),金色动物退化为原色。");
                done?.Invoke();
                return;
            }

            res.LoadAsync<Shader>(GoldShaderPath, shader =>
            {
                if (goldMaterial == null) // 防并发重复创建
                {
                    if (shader == null) shader = Shader.Find("Custom/AnimalGold");
                    if (shader == null)
                        Debug.LogWarning($"[AnimalSystem] 找不到金色泛光 shader(Resources/{GoldShaderPath} 或 Custom/AnimalGold),金色动物退化为原色。");
                    else
                        goldMaterial = new Material(shader) { name = "AnimalGold (runtime)" };
                }
                done?.Invoke();
            });
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
