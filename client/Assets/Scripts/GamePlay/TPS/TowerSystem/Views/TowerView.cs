using UnityEngine;

/// <summary>
/// 防御塔 view：被动同步 Tower.Position / Tower.Rotation 到 transform。
/// Tower 整体旋转（仅一个 transform）—— TowerWeaponComponent 写 Owner.Rotation，view 这里读着用。
///
/// 武器模型挂载不在这里，由 WeaponView 通过 ViewManager.TryGetView 反查本 TowerView 的 socket 子物体来 reparent。
/// </summary>
public class TowerView : BaseView
{
    private Tower tower;

    public override void Bind(Actor actor, int id)
    {
        tower = actor as Tower;
        ID = id;
        if (tower != null)
        {
            tower.Position = transform.position;
            tower.Rotation = transform.rotation;
        }
    }

    private void OnDestroy()
    {
        tower = null;
        ID = -1;
    }

    private void LateUpdate()
    {
        if (tower == null) return;
        transform.position = tower.Position;
        transform.rotation = tower.Rotation;
        ApplyFogVisibility(); // 战争迷雾遮挡剔除：读 Owner.Visible 开关自身 renderer
    }
}
