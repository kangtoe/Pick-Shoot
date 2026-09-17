using UnityEngine;
using UnityEngine.Serialization;

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
        // 1회 사격의 탄환 수 = 소비 탄약 수 (Weapons §4). 탄약이 모자라면 남은 만큼만 나간다.
        [SerializeField, Min(1)] int projectilesPerShot = 1;
        [SerializeField, Range(0f, 180f)] float spreadAngleDegrees = 0f;
        [SerializeField] int damage = 10;
        [SerializeField] float knockbackForce = 6f;
        [SerializeField, Min(0), FormerlySerializedAs("maxEnemyHits")] int maxBounces = 0;
        [SerializeField, Min(0)] int penetration = 0;
        [SerializeField] Color visualColor = Color.white;

        [Header("Presentation")]
        [SerializeField] Sprite weaponSprite;
        [SerializeField] AudioClip shootClip;
        // 무기 중심에서 총구 화염이 뜨는 지점까지의 거리 (스프라이트 +x 방향, 월드 단위).
        [SerializeField, Min(0f)] float muzzleOffset = 0.5f;
        // 반동 연출 배율. PlayerWeapons의 반동 거리·각도에 곱한다 (무거운 무기일수록 크게).
        [SerializeField, Min(0f)] float recoilStrength = 1f;

        public string DisplayName => displayName;
        public Bullet BulletPrefab => bulletPrefab;
        public float BulletSpeed => bulletSpeed;
        public float Cooldown => cooldown;
        public int MaxAmmo => maxAmmo;
        public int ProjectilesPerShot => projectilesPerShot;
        public float SpreadAngleDegrees => spreadAngleDegrees;
        public int Damage => damage;
        public float KnockbackForce => knockbackForce;
        public int MaxBounces => maxBounces;
        public int Penetration => penetration;
        public Color VisualColor => visualColor;
        public Sprite WeaponSprite => weaponSprite;
        public AudioClip ShootClip => shootClip;
        public float MuzzleOffset => muzzleOffset;
        public float RecoilStrength => recoilStrength;
    }
}
