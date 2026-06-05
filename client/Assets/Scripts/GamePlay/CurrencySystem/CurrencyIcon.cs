using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YOTO
{
    /// <summary>
    /// 金币/体力图标的统一来源:图标放在 <c>Resources/UI/Icons</c> 下,运行时按币种动态加载,
    /// 方便后续直接替换 Resources 里的图(无需重建任何预制体)。各面板资源胶囊的图标由
    /// <see cref="CurrencyIconBinder"/> 在运行时贴上,不再把 Sprite 烤进预制体。
    /// </summary>
    public static class CurrencyIcon
    {
        public const string GoldResPath = "UI/Icons/coin_2";
        public const string EnergyResPath = "UI/Icons/energy";

        /// <summary>币种 → Resources 图标路径(金币及未知币种默认用金币图)。</summary>
        public static string ResPath(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Energy: return EnergyResPath;
                default: return GoldResPath;
            }
        }

        /// <summary>
        /// 按币种异步加载图标:优先走框架 <see cref="ResMgr"/>(带缓存)。<see cref="GameLoop"/> 上下文尚未就绪
        /// (如 MonoBehaviour 的 Awake 阶段)时退 <c>Resources.Load</c> 同步兜底。找不到回 null。
        /// </summary>
        public static void LoadAsync(CurrencyType type, Action<Sprite> onReady)
        {
            if (onReady == null) return;
            var path = ResPath(type);
            if (string.IsNullOrEmpty(path)) { onReady(null); return; }
            var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
            if (ctx != null && ctx.TryGet<ResMgr>(out var res) && res != null)
                res.LoadAsync<Sprite>(path, onReady);
            else
                onReady(Resources.Load<Sprite>(path)); // ctx 未就绪:同步兜底(仅 Resources 后端)
        }

        /// <summary>
        /// 把对应币种图标贴到「资源胶囊」里的 Icon 上(运行时从 Resources 取)。胶囊结构:数值文本与 Icon 同为胶囊的直接子物体,
        /// 故从数值文本找兄弟节点 "Icon" 即可。供尚未重建(没有 <see cref="CurrencyIconBinder"/>)的旧预制体在面板 OnLoad 时调用。
        /// </summary>
        public static void Bind(TextMeshProUGUI pillValue, CurrencyType type)
        {
            if (pillValue == null || pillValue.transform.parent == null) return;
            var iconTf = pillValue.transform.parent.Find("Icon");
            if (iconTf == null) return;
            var img = iconTf.GetComponent<Image>();
            if (img == null) return;
            LoadAsync(type, s => { if (img != null && s != null) { img.sprite = s; img.enabled = true; } });
        }
    }
}
