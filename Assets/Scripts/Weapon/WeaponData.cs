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
        // 사격 차례 사이의 쿨다운(초). 레벨로 줄지 않는 무기 특성이며 PlayerWeapons의 글로벌 최소 발사 간격 이상이어야 한다 (Weapons §1·§3).
        [SerializeField] float cooldown = 0.2f;
        [SerializeField] int maxAmmo = 6;
        // 레벨업 한 번마다 최대 탄약에 더하는 수 (누적). 표준 2. 0이면 고정 탄창 — 탄창 크기가 정체성인 무기 (Weapons §3·§9).
        [SerializeField, Min(0)] int levelMaxAmmoAdd = 2;
        // 1회 사격의 탄환 수 = 소비 탄약 수 (Weapons §4). 탄약이 모자라면 남은 만큼만 나간다.
        [SerializeField, Min(1)] int projectilesPerShot = 1;
        [SerializeField, Range(0f, 180f)] float spreadAngleDegrees = 0f;
        // 한 번의 사격 차례에 이어 쏘는 사격 수. 2 이상이면 점사 — burstInterval 간격으로 이어 쏘고, 사격마다 projectilesPerShot만큼 탄약을 쓴다 (Weapons §8). 탄창을 넘는 값(블래스터 99)은 탄창이 빌 때까지 이어 쏘는 뜻이다.
        [SerializeField, Min(1)] int burstCount = 1;
        // 점사 안의 사격 간격(초). burstCount가 1이면 쓰지 않는다.
        [SerializeField, Min(0f)] float burstInterval = 0.07f;
        [SerializeField] int damage = 10;
        // 레벨업 한 번마다 공격력에 곱하는 배율 (누적). 표준 1.5. 1이면 공격력이 레벨로 오르지 않는다 (Weapons §3).
        [SerializeField, Min(1f)] float levelDamageMultiplier = 1.5f;
        [SerializeField] float knockbackForce = 6f;
        [SerializeField, Min(0), FormerlySerializedAs("maxEnemyHits")] int maxBounces = 0;
        [SerializeField, Min(0)] int penetration = 0;
        // 0이면 보통 탄환. 양수면 폭발 탄환 — 튕김 페이즈의 접촉 지점을 중심으로 이 반경(월드 단위) 안의 모든 적에게 damage·knockbackForce를 준다 (Weapons §7).
        [SerializeField, Min(0f)] float explosionRadius = 0f;
        // 참이면 폭발 넉백이 바깥으로 미는 대신 반경 안의 적을 중심으로 당긴다 (Weapons §7). explosionRadius > 0일 때만 뜻이 있다.
        [SerializeField] bool explosionPullsInward = false;
        // 참이면 탄환이 정착하는 순간 거리와 무관하게 플레이어 자석에 걸려 돌아온다 (Weapons §5·§9).
        [SerializeField] bool autoRetrieve = false;
        [SerializeField] Color visualColor = Color.white;

        [Header("Presentation")]
        [SerializeField] Sprite weaponSprite;
        [SerializeField] AudioClip shootClip;
        // 폭발 탄환의 폭발음. 접촉 지점에서 튕김음·명중 파문 대신 난다.
        [SerializeField] AudioClip explosionClip;
        // 무기 중심에서 총구 화염이 뜨는 지점까지의 거리 (스프라이트 +x 방향, 월드 단위).
        [SerializeField, Min(0f)] float muzzleOffset = 0.5f;
        // 반동 연출 배율. PlayerWeapons의 반동 거리·각도에 곱한다 (무거운 무기일수록 크게).
        [SerializeField, Min(0f)] float recoilStrength = 1f;

        public string DisplayName => displayName;
        public Bullet BulletPrefab => bulletPrefab;
        public float BulletSpeed => bulletSpeed;
        public float Cooldown => cooldown;
        public int MaxAmmo => maxAmmo;
        public int LevelMaxAmmoAdd => levelMaxAmmoAdd;
        public int ProjectilesPerShot => projectilesPerShot;
        public float SpreadAngleDegrees => spreadAngleDegrees;
        public int BurstCount => burstCount;
        public float BurstInterval => burstInterval;
        public int Damage => damage;
        public float LevelDamageMultiplier => levelDamageMultiplier;
        public float KnockbackForce => knockbackForce;
        public int MaxBounces => maxBounces;
        public int Penetration => penetration;
        public float ExplosionRadius => explosionRadius;
        public bool ExplosionPullsInward => explosionPullsInward;
        public bool AutoRetrieve => autoRetrieve;
        public Color VisualColor => visualColor;
        public Sprite WeaponSprite => weaponSprite;
        public AudioClip ShootClip => shootClip;
        public AudioClip ExplosionClip => explosionClip;
        public float MuzzleOffset => muzzleOffset;
        public float RecoilStrength => recoilStrength;
    }
}
