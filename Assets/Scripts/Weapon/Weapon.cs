using UnityEngine;

namespace PickShoot
{
    // 무기 인스턴스 런타임 상태. WeaponData(정의) + 현재 탄창 + 다음 준비 시각.
    public class Weapon
    {
        readonly WeaponData data;
        int currentAmmo;
        float nextReadyTime;

        public WeaponData Data => data;
        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => data.MaxAmmo;
        public float NextReadyTime => nextReadyTime;

        public Weapon(WeaponData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] Weapon 생성자에 WeaponData가 null.");
            if (data.BulletPrefab == null)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'에 BulletPrefab 미할당.");
            this.data = data;
            currentAmmo = data.MaxAmmo;
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
                throw new System.Exception($"[PickShoot] Weapon.Fire 호출됐지만 CanFire가 false. weapon={data.DisplayName}, now={now}, nextReadyTime={nextReadyTime}, ammo={currentAmmo}/{data.MaxAmmo}");

            currentAmmo -= data.AmmoPerShot;
            nextReadyTime = now + data.Cooldown;

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

                bullet.Launch(dir * data.BulletSpeed, this, data.Damage, data.KnockbackForce);
            }
        }

        // 회수 시 이 무기의 탄창에 1발 추가. 이미 가득 찼으면 false.
        public bool TryAddAmmo()
        {
            if (currentAmmo >= data.MaxAmmo) return false;
            currentAmmo++;
            return true;
        }
    }
}
