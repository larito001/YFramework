using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Combat/WeaponConfig")]
    public class WeaponConfigSO : ScriptableObject
    {
        [Header("Common")]
        public float fireRate = 6f;               // 每秒发射次数
        public float range = 60f;
        public LayerMask hitMask = ~0;            // 命中层
        public bool hitscan = true;

        [Header("Spread/Recoil")]
        public float spreadDegrees = 0.5f;

        [Header("Projectile")]
        public float projectileSpeed = 80f;
        public float projectileRadius = 0.05f;

        [Header("Damage")]
        public DamageType damageType = DamageType.Kinetic;
        public float damage = 10f;
        public float critChance = 0.1f;
        public float critMultiplier = 2.0f;
        public float armorPenetration = 0.0f;

        [Header("Status Effects (optional)")]
        public bool applyBurn;
        public float burnDps = 2f;
        public float burnDuration = 3f;

        public DamageSpec BuildDamageSpec()
        {
            return new DamageSpec
            {
                Amount = damage,
                Type = damageType,
                CritChance = critChance,
                CritMultiplier = critMultiplier,
                ArmorPenetration = armorPenetration
            };
        }
    }
}