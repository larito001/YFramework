using UnityEngine;
using YFramework.Config;
using YOTO;

/// <summary>
/// 第一人称手持武器视图模型:挂在主相机上,把当前出战武器(<see cref="LoadoutSystem"/> 选中的枪)的模型
/// 作为相机子物体放在镜头前下方,像 FPS 那样"端在手里"。换装(<see cref="YOTOEventType.RefreshLoadout"/>)时自动更换。
///
/// 由 <see cref="GameStartScene"/> 进对局时 AddComponent 并 <see cref="Init"/> 注入 <see cref="GameContext"/>。
/// 手持的局部位置/旋转/缩放是公开字段,觉得位置不对直接调这几个常量即可。
/// </summary>
public class FpsWeaponViewModel : MonoBehaviour
{
    [Header("手持视角:相对相机的局部变换(觉得不对就调这几个)")]
    public Vector3 localPosition = new Vector3(0.22f, -0.29f, 0.42f);    // 右、下、前(编辑器内实测值)
    public Vector3 localEuler = new Vector3(-5f, 77.48f, -12.57f);       // 枪口朝向(编辑器内实测值)
    public float scale = 1f;

    [Header("开枪后座(代码模拟:冲击后退上抬 + 平滑复位)")]
    public float kickBack = 0.06f;      // 每枪向后位移(米)
    public float kickUp = 6f;           // 每枪枪口上抬(度)
    public float kickRandomYaw = 2.5f;  // 每枪左右随机偏摆(度)
    public float snappiness = 18f;      // 后座到位速度(越大越脆)
    public float returnSpeed = 9f;      // 复位速度(越大回得越快)

    private LoadoutSystem loadout;
    private ConfigManager config;
    private ResMgr res;
    private EventMgr eventMgr;
    private ScopeAimController scopeAim; // 同挂主相机:开镜时据此隐藏手持枪

    private GameObject model;
    private int shownId = -1;
    private int refreshVersion; // 每次 Refresh 自增:异步加载回调比对,过期则丢弃(防快速换枪覆盖)
    private bool forceHidden; // 结束打猎尸检镜头期间强制隐藏手持枪

    /// <summary>强制隐藏/显示手持枪(尸检镜头用;进对局 <see cref="Init"/> 会复位为显示)。</summary>
    public void SetForceHidden(bool hidden) => forceHidden = hidden;

    private Vector3 recoilPos, recoilEuler;             // 当前后座偏移(叠加在 rest 之上)
    private Vector3 targetRecoilPos, targetRecoilEuler; // 目标后座(开枪冲击后向 0 衰减)

    /// <summary>注入服务并开始随换装刷新。GameStartScene 在挂载后调用。</summary>
    public void Init(GameContext ctx)
    {
        forceHidden = false; // 每次进对局复位:正常显示手持枪
        loadout = ctx.Get<LoadoutSystem>();
        config = ctx.Get<ConfigManager>();
        res = ctx.Get<ResMgr>();
        eventMgr = ctx.Get<EventMgr>();
        scopeAim = GetComponent<ScopeAimController>();

        eventMgr?.Add(YOTOEventType.RefreshLoadout, Refresh);
        eventMgr?.Add(YOTOEventType.Shoot, OnShoot); // 开枪后座
        Refresh();
    }

    private void OnDestroy()
    {
        eventMgr?.Remove(YOTOEventType.RefreshLoadout, Refresh);
        eventMgr?.Remove(YOTOEventType.Shoot, OnShoot);
        if (model != null) Destroy(model);
    }

    /// <summary>每帧把后座偏移弹簧式逼近并衰减回 0,叠加到手持 rest 姿势上。</summary>
    private void Update()
    {
        if (model == null) return;

        // 开镜(瞄准镜)时隐藏手持枪,退出瞄准再显示;尸检镜头期间强制隐藏
        bool show = !forceHidden && (scopeAim == null || !scopeAim.IsAiming);
        if (model.activeSelf != show) model.SetActive(show);

        float dt = Time.deltaTime;

        // 目标后座向 0 衰减(恢复);当前后座向目标快速逼近(冲击)→ 脆击 + 平滑回复
        targetRecoilPos = Vector3.Lerp(targetRecoilPos, Vector3.zero, returnSpeed * dt);
        targetRecoilEuler = Vector3.Lerp(targetRecoilEuler, Vector3.zero, returnSpeed * dt);
        recoilPos = Vector3.Lerp(recoilPos, targetRecoilPos, snappiness * dt);
        recoilEuler = Vector3.Lerp(recoilEuler, targetRecoilEuler, snappiness * dt);

        model.transform.localPosition = localPosition + recoilPos;
        model.transform.localEulerAngles = localEuler + recoilEuler;
    }

    /// <summary>每次开枪(<see cref="YOTOEventType.Shoot"/>)给一记后座冲击:向后 + 枪口上抬 + 轻微随机偏摆。</summary>
    private void OnShoot()
    {
        targetRecoilPos += new Vector3(0f, 0f, -kickBack); // 向相机方向(后)退
        targetRecoilEuler += new Vector3(-kickUp, Random.Range(-kickRandomYaw, kickRandomYaw), 0f); // -X=枪口上抬
    }

    /// <summary>按当前出战武器的 item.ModelPath 换上手持模型;无模型则不显示。</summary>
    private void Refresh()
    {
        int id = loadout != null ? loadout.GetSelected(ShopCategory.Weapon) : 0;
        var item = id > 0 ? config?.itemConfig.Get((uint)id) : null;
        string path = item != null ? item.ModelPath : null;

        if (string.IsNullOrEmpty(path))
        {
            if (model != null) { Destroy(model); model = null; }
            shownId = -1;
            refreshVersion++; // 取消在途加载
            return;
        }

        if (id == shownId && model != null) return; // 没变

        if (model != null) { Destroy(model); model = null; }
        shownId = id;                   // 立即占位
        int v = ++refreshVersion;
        if (res == null) return;
        res.LoadAsync<GameObject>(path, prefab =>
        {
            if (v != refreshVersion || this == null) // 期间又换枪 / 对象已销毁:丢弃并配平
            {
                if (prefab != null) res.Release<GameObject>(path);
                return;
            }
            if (prefab == null)
            {
                Debug.LogWarning($"[FpsWeaponViewModel] 武器模型未找到(确认已在 Resources/ 下): {path}");
                return;
            }

            Debug.Log($"[FpsWeaponViewModel] 出战武器 id={id} 手持模型={path}");
            model = Instantiate(prefab, transform); // 作为相机子物体
            res.Release<GameObject>(path);           // 实例已建,释放 prefab 引用(配平 LoadAsync 的 +1)
            WeaponModelUtil.DisableColliders(model); // 去碰撞体,避免挡住自己的射线/物理
            var t = model.transform;
            t.localPosition = localPosition;
            t.localEulerAngles = localEuler;
            t.localScale = Vector3.one * scale;
            recoilPos = recoilEuler = targetRecoilPos = targetRecoilEuler = Vector3.zero; // 换枪清掉残余后座
        });
    }
}
