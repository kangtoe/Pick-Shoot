using UnityEngine;

namespace PickShoot
{
    [CreateAssetMenu(menuName = "PickShoot/Weapon Data", fileName = "WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] string displayName = "Weapon";
        [SerializeField] Bullet bulletPrefab;
        [SerializeField] float bulletSpeed = 14f;
        [SerializeField] float cooldown = 0.2f;
        [SerializeField] int maxAmmo = 6;
        [SerializeField, Min(1)] int ammoPerShot = 1;
        [SerializeField, Min(1)] int projectilesPerShot = 1;
        [SerializeField, Range(0f, 180f)] float spreadAngleDegrees = 0f;
        [SerializeField] int damage = 1;
        [SerializeField] float knockbackForce = 6f;
        [SerializeField] Color visualColor = Color.white;

        public string DisplayName => displayName;
        public Bullet BulletPrefab => bulletPrefab;
        public float BulletSpeed => bulletSpeed;
        public float Cooldown => cooldown;
        public int MaxAmmo => maxAmmo;
        public int AmmoPerShot => ammoPerShot;
        public int ProjectilesPerShot => projectilesPerShot;
        public float SpreadAngleDegrees => spreadAngleDegrees;
        public int Damage => damage;
        public float KnockbackForce => knockbackForce;
        public Color VisualColor => visualColor;
    }
}
