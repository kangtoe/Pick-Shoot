using UnityEngine;

namespace PickShoot
{
    // 무기 인스턴스 런타임 상태. WeaponData(정의) + 현재 탄창 + 다음 준비 시각 + 레벨.
    // 강화는 자루 단위 레벨업 단일 축 (GDD §7.3). 레벨업 시 3항목이 함께 상승한다:
    //   damage ×1.5, cooldown ×0.75, maxAmmo +2 (각 레벨업당 누적).
    // 새로 획득한 자루는 항상 Lv.1로 시작.
    public class Weapon
    {
        public const float LevelDamageMultiplier = 1.5f;
        public const float LevelCooldownMultiplier = 0.75f;
        public const int LevelMaxAmmoAdd = 2;

        readonly WeaponData data;
        int level = 1;
        int currentAmmo;
        float nextReadyTime;

        public WeaponData Data => data;
        public int Level => level;
        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => MaxAmmoAtLevel(data, level);
        public float NextReadyTime => nextReadyTime;
        public int EffectiveDamage => DamageAtLevel(data, level);
        public float EffectiveCooldown => CooldownAtLevel(data, level);

        // 레벨별 수치 공식. 업그레이드 카드가 "다음 레벨" 값을 미리 보여줄 때도 같은 식을 쓴다.
        public static int DamageAtLevel(WeaponData data, int level)
        {
            return Mathf.Max(1, Mathf.RoundToInt(data.Damage * Mathf.Pow(LevelDamageMultiplier, level - 1)));
        }

        public static float CooldownAtLevel(WeaponData data, int level)
        {
            return Mathf.Max(0.01f, data.Cooldown * Mathf.Pow(LevelCooldownMultiplier, level - 1));
        }

        public static int MaxAmmoAtLevel(WeaponData data, int level)
        {
            return data.MaxAmmo + (level - 1) * LevelMaxAmmoAdd;
        }

        public Weapon(WeaponData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] Weapon 생성자에 WeaponData가 null.");
            if (data.BulletPrefab == null)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'에 BulletPrefab 미할당.");
            if (data.WeaponSprite == null)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'에 WeaponSprite 미할당. 링 위 무기 시각화용 sprite 필요.");
            this.data = data;
            currentAmmo = MaxAmmo;
            nextReadyTime = 0f;
        }

        public bool CanFire(float now)
        {
            return now >= nextReadyTime && currentAmmo >= data.AmmoPerShot;
        }

        // 발사 실행. 호출자가 CanFire를 먼저 검증하고, 방향/위치/탄환 콜리전 무시 처리를 수행한다.
        // fireDirection은 정규화되어 있어야 한다.
        public void Fire(Vector3 muzzlePos, Vector2 fireDirection, Collider2D shooterCollider, float now)
        {
            if (!CanFire(now))
                throw new System.Exception($"[PickShoot] Weapon.Fire 호출됐지만 CanFire가 false. weapon={data.DisplayName} Lv{level}, now={now}, nextReadyTime={nextReadyTime}, ammo={currentAmmo}/{MaxAmmo}");

            currentAmmo -= data.AmmoPerShot;
            nextReadyTime = now + EffectiveCooldown;
            if (data.ShootClip != null) SoundPlayer.Play(data.ShootClip);

            int shots = data.ProjectilesPerShot;
            float spread = data.SpreadAngleDegrees;
            float baseAngle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;

            for (int i = 0; i < shots; i++)
            {
                float angle = baseAngle;
                if (shots > 1 && spread > 0f)
                {
                    float t = shots == 1 ? 0.5f : (float)i / (shots - 1);
                    angle = baseAngle - spread * 0.5f + spread * t;
                }

                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                var bullet = Object.Instantiate(data.BulletPrefab, muzzlePos, Quaternion.identity);
                var bulletCol = bullet.GetComponent<Collider2D>();
                if (bulletCol == null)
                    throw new System.Exception($"[PickShoot] '{data.DisplayName}' bulletPrefab에 Collider2D 없음.");
                if (shooterCollider != null)
                    Physics2D.IgnoreCollision(shooterCollider, bulletCol, true);

                bullet.Launch(dir * data.BulletSpeed, this, EffectiveDamage, data.KnockbackForce, data.MaxBounces, data.Penetration);
            }
        }

        // 회수 시 이 무기의 탄창에 1발 추가. 이미 가득 찼으면 false.
        public bool TryAddAmmo()
        {
            if (currentAmmo >= MaxAmmo) return false;
            currentAmmo++;
            return true;
        }

        // 자루 단위 레벨업 (GDD §7.3). 최대 탄약 상승분만큼 현재 탄약도 함께 충전한다.
        public void LevelUp()
        {
            level++;
            currentAmmo = Mathf.Min(currentAmmo + LevelMaxAmmoAdd, MaxAmmo);
        }
    }
}
