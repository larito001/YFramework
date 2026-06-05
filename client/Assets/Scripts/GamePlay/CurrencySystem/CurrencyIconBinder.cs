using UnityEngine;
using UnityEngine.UI;

namespace YOTO
{
    /// <summary>
    /// 挂在「金币/体力」图标 Image 上:运行时按 <see cref="type"/> 从 Resources 动态加载图标并赋给本 Image
    /// (见 <see cref="CurrencyIcon"/>)。这样各面板顶部资源胶囊的图标不再烤进预制体,换图只需替换
    /// <c>Resources/UI/Icons</c> 下的图。由各面板生成器在搭胶囊时挂上并设好 <see cref="type"/>。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CurrencyIconBinder : MonoBehaviour
    {
        public CurrencyType type = CurrencyType.Gold;

        private void Awake() => Apply();

        /// <summary>从 Resources 取对应币种图标贴到本 Image(取不到则保持原样,不清空)。</summary>
        public void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null) return;
            var s = CurrencyIcon.Load(type);
            if (s != null) { img.sprite = s; img.enabled = true; }
        }
    }
}
