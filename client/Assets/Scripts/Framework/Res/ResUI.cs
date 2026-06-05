using System;
using UnityEngine;
using UnityEngine.UI;

namespace YOTO
{
    /// <summary>
    /// UI 资源加载辅助:把"异步加载 Sprite 并赋给 <see cref="Image"/>"这件高频事统一起来 ——
    /// 先占位、加载完赋图、目标已销毁则丢弃并配平引用计数。资源系统异步化后(为 Addressables 铺路),
    /// 各面板填图标不再写散落的回调样板,统一走这里。
    /// </summary>
    public static class ResUI
    {
        /// <summary>
        /// 异步把 <paramref name="path"/> 的 Sprite 赋给 <paramref name="img"/>。先把图清空并置 <paramref name="placeholder"/> 占位色,
        /// 加载完成赋图并恢复白色;path 为空直接占位。<paramref name="alive"/> 可选额外存活判定(如卡片是否已被回收),
        /// 回调时目标已销毁(Unity 假 null)或不再存活则丢弃结果并 <see cref="ResMgr.Release{T}"/> 配平。
        /// </summary>
        public static void SetSpriteAsync(ResMgr res, Image img, string path, Color placeholder, Func<bool> alive = null)
        {
            if (img == null)
            {
                return;
            }

            if (res == null || string.IsNullOrEmpty(path))
            {
                img.sprite = null;
                img.color = placeholder;
                return;
            }

            img.sprite = null;
            img.color = placeholder;

            res.LoadAsync<Sprite>(path, sp =>
            {
                if (img == null || (alive != null && !alive()))
                {
                    if (sp != null)
                    {
                        res.Release<Sprite>(path);
                    }

                    return;
                }

                if (sp != null)
                {
                    img.sprite = sp;
                    img.color = Color.white;
                }
                else
                {
                    img.color = placeholder;
                }
            });
        }
    }
}
