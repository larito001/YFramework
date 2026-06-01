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

    private LoadoutSystem loadout;
    private ConfigManager config;
    private ResMgr res;
    private EventMgr eventMgr;

    private GameObject model;
    private int shownId = -1;

    /// <summary>注入服务并开始随换装刷新。GameStartScene 在挂载后调用。</summary>
    public void Init(GameContext ctx)
    {
        loadout = ctx.Get<LoadoutSystem>();
        config = ctx.Get<ConfigManager>();
        res = ctx.Get<ResMgr>();
        eventMgr = ctx.Get<EventMgr>();

        eventMgr?.Add(YOTOEventType.RefreshLoadout, Refresh);
        Refresh();
    }

    private void OnDestroy()
    {
        eventMgr?.Remove(YOTOEventType.RefreshLoadout, Refresh);
        if (model != null) Destroy(model);
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
            return;
        }

        if (id == shownId && model != null) return; // 没变

        if (model != null) { Destroy(model); model = null; }

        var prefab = res != null ? res.Load<GameObject>(path) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"[FpsWeaponViewModel] 武器模型未找到(确认已在 Resources/ 下): {path}");
            return;
        }

        model = Instantiate(prefab, transform); // 作为相机子物体
        WeaponModelUtil.DisableColliders(model); // 去碰撞体,避免挡住自己的射线/物理
        var t = model.transform;
        t.localPosition = localPosition;
        t.localEulerAngles = localEuler;
        t.localScale = Vector3.one * scale;
        shownId = id;
    }
}
