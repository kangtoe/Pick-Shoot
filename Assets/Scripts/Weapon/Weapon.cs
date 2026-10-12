using UnityEngine;

namespace PickShoot
{
    // 무기 인스턴스 런타임 상태. WeaponData(정의) + 현재 탄창 + 다음 준비 시각 + 레벨 + 점사 잔량.
    // 강화는 자루 단위 레벨업 단일 축 (GDD §7.3). 레벨업 시 공격력과 최대 탄약이 함께 오르고, 레벨당 증가값(공격력 배율·탄창 가산)은
    //   무기별로 WeaponData가 정한다 — 표준 ×1.5·+2, 탄창 가산 0이면 고정 탄창 (Weapons §3).
    // 쿨다운은 레벨과 무관한 무기 특성이라 오르지 않는다 (Weapons §3).
    // 새로 획득한 자루는 항상 Lv.1로 시작하고 MaxLevel까지 오른다 (Weapons §3).
    // 피해에는 플레이어 전역 배율(PlayerStats의 공격력, 유물 — Relics §2)이 곱해진다. Weapon은 플레이어를 모르므로 발사 때 호출자(PlayerWeapons)가 넘긴다.
    // 개조(ModData, Mods §1)는 자루당 하나, 붙으면 런이 끝날 때까지 그대로다. 새 자루는 개조 없이 들어오고, 레벨업해도 유지된다.
    // 개조가 더하는 수치(튕김·관통·폭발 반경)는 ...With 함수가 자산 값에 얹어 계산한다 — 카드가 붙인 뒤의 값을 미리 보일 때도 같은 식을 쓴다.
    public class Weapon
    {
        // 레벨 상한. 한 자리 수로 묶어 HUD·카드의 레벨 표기가 늘 "LV n" 한 자리이고, 최대 탄약(HUD 탄창 줄 길이)도 이 안에 머문다.
        public const int MaxLevel = 9;

        readonly WeaponData data;
        int level = 1;
        int currentAmmo;
        float nextReadyTime;
        // 시작한 점사에서 아직 쏘지 않은 사격 수. 0이면 점사 중이 아니다 (Weapons §8).
        int burstShotsLeft;
        ModData mod;

        public WeaponData Data => data;
        // 붙은 개조. 없으면 null.
        public ModData Mod => mod;
        public int Level => level;
        public bool IsMaxLevel => level >= MaxLevel;
        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => MaxAmmoAtLevel(data, level);
        // 점사 중 — burstInterval 뒤 CanFire가 다시 참이 되면 호출자가 사격 버튼과 무관하게 다시 Fire한다 (Weapons §8).
        public bool IsBursting => burstShotsLeft > 0;
        // 쿨다운은 레벨로 줄지 않는다 — 사격 리듬은 무기의 특성 (Weapons §3).
        public float EffectiveCooldown => data.Cooldown;

        // 레벨별 수치 공식. 업그레이드 카드가 "다음 레벨" 값을 미리 보여줄 때도 같은 식을 쓴다.
        // 피해 = (자산 값 + 레벨 가산 × (레벨−1)) × 플레이어 전역 배율 — 레벨 증가는 고정 가산이라 레벨당 오르는 폭이 늘 같다 (2026-10-07, 그전까지는 ×1.5 누적 곱셈).
        // 전역 배율까지 곱한 뒤 한 번만 반올림한다. 그래도 피해 5~10인 무기는 +10%가 반올림에 묻히기 쉽다 (Relics §2, 튜닝 대상).
        public static int DamageAtLevel(WeaponData data, int level, float globalMultiplier)
        {
            if (globalMultiplier <= 0f)
                throw new System.Exception($"[PickShoot] Weapon.DamageAtLevel: 전역 피해 배율이 0 이하 ({globalMultiplier}).");
            return Mathf.Max(1, Mathf.RoundToInt((data.Damage + data.LevelDamageAdd * (level - 1)) * globalMultiplier));
        }

        public static int MaxAmmoAtLevel(WeaponData data, int level)
        {
            return data.MaxAmmo + (level - 1) * data.LevelMaxAmmoAdd;
        }

        // 한 사격 차례의 사격 수 — 점사 수가 그 레벨의 탄창으로 쏠 수 있는 사격 수를 넘으면 탄창이 빌 때까지가 한 차례다 (Weapons §8, 블래스터).
        public static int ShotsPerTriggerAtLevel(WeaponData data, int level)
        {
            int shotsInMagazine = Mathf.CeilToInt((float)MaxAmmoAtLevel(data, level) / data.ProjectilesPerShot);
            return Mathf.Min(data.BurstCount, shotsInMagazine);
        }

        // 한 번의 사격에 나가는 탄환 수 = 동시 발사 수(산탄), 그 레벨의 탄창이 상한. 카드의 DMG가 'X발수'로 보여준다.
        // 점사 수는 곱하지 않는다 (2026-10-07) — 카드에 BURST 줄이 따로 서므로 곱하면 같은 수가 두 번 들어가고, 블래스터 고레벨의
        // "108X24" 같은 값은 카드 폭을 넘긴다.
        public static int BulletsPerShotAtLevel(WeaponData data, int level)
        {
            return Mathf.Min(data.ProjectilesPerShot, MaxAmmoAtLevel(data, level));
        }

        // 개조가 더한 튕김 횟수·관통 (Mods §2.3·§2.4). mod가 null이면 자산 값.
        public static int BouncesWith(WeaponData data, ModData mod)
        {
            return data.MaxBounces + (mod != null ? mod.BounceAdd : 0);
        }

        public static int PenetrationWith(WeaponData data, ModData mod)
        {
            return data.Penetration + (mod != null ? mod.PenetrationAdd : 0);
        }

        // 개조를 얹은 폭발 반경 (Mods §2.1). 폭발 개조는 폭발 탄환이 아닌 무기에 개조의 반경을 주어 런처와 같은 폭발 탄환으로 바꾸고,
        // 폭발 탄환 무기의 반경에는 배율을 곱한다 (런처 2 → 3). 0이면 보통 탄환.
        public static float ExplosionRadiusWith(WeaponData data, ModData mod)
        {
            if (mod == null) return data.ExplosionRadius;
            if (mod.MakesExplosive(data)) return mod.ExplosionRadius;
            return data.ExplosionRadius * mod.ExplosionRadiusMultiplier;
        }

        public Weapon(WeaponData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] Weapon 생성자에 WeaponData가 null.");
            if (data.BulletPrefab == null)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'에 BulletPrefab 미할당.");
            if (data.WeaponSprite == null)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'에 WeaponSprite 미할당. 링 위 무기 시각화용 sprite 필요.");
            if (data.ExplosionPullsInward && data.ExplosionRadius <= 0f)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'는 당기는 폭발(explosionPullsInward)인데 explosionRadius가 0. 폭발 탄환이어야 함.");
            // 쿨다운 0이면 매 프레임 쏘게 되어 사격 빈도가 프레임레이트를 따른다.
            if (data.Cooldown <= 0f)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'의 cooldown이 0 이하 ({data.Cooldown}). 쿨다운은 양수여야 함 (Weapons §1).");
            if (data.BurstCount > 1 && data.BurstInterval <= 0f)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'는 점사({data.BurstCount})인데 burstInterval이 0 이하 ({data.BurstInterval}). 점사 간격은 양수여야 함.");
            if (data.LevelDamageAdd < 0)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'의 levelDamageAdd가 음수 ({data.LevelDamageAdd}). 레벨업으로 공격력이 줄 수는 없음 (Weapons §3).");
            if (data.LevelMaxAmmoAdd < 0)
                throw new System.Exception($"[PickShoot] WeaponData '{data.DisplayName}'의 levelMaxAmmoAdd가 음수 ({data.LevelMaxAmmoAdd}). 0(고정 탄창) 이상이어야 함 (Weapons §3).");
            this.data = data;
            currentAmmo = MaxAmmo;
            nextReadyTime = 0f;
        }

        // 탄약이 한 번 사격분(ProjectilesPerShot)에 못 미쳐도 1발이라도 있으면 쏠 수 있다 (Weapons §4).
        public bool CanFire(float now)
        {
            return now >= nextReadyTime && currentAmmo > 0;
        }

        // 발사 실행 — 한 번의 사격(projectilesPerShot발). 호출자가 CanFire를 먼저 검증하고, 방향/위치/탄환 콜리전 무시 처리를 수행한다.
        // fireDirection은 정규화되어 있어야 한다.
        // 점사 무기(BurstCount > 1)는 첫 호출이 점사를 시작하고 burstInterval 뒤에 다시 준비되므로, 호출자가 IsBursting인 자루를
        // 사격 버튼과 무관하게 다시 Fire하면 점사가 이어진다. 쿨다운은 점사의 마지막 사격 뒤에 시작하고, 탄약이 떨어지면 점사도 거기서 끝난다 (Weapons §8).
        // damageMultiplier는 플레이어 전역 피해 배율 — 이 사격의 탄환(폭발 피해 포함)에 곱해진다.
        public void Fire(Vector3 muzzlePos, Vector2 fireDirection, Collider2D shooterCollider, float now, float damageMultiplier)
        {
            if (!CanFire(now))
                throw new System.Exception($"[PickShoot] Weapon.Fire 호출됐지만 CanFire가 false. weapon={data.DisplayName} Lv{level}, now={now}, nextReadyTime={nextReadyTime}, ammo={currentAmmo}/{MaxAmmo}");
            int damage = DamageAtLevel(data, level, damageMultiplier);

            // 점사 이어 쏘기면 잔량을 하나 줄이고, 아니면 새 점사를 연다 (점사 무기가 아니면 잔량 0).
            if (burstShotsLeft > 0) burstShotsLeft--;
            else burstShotsLeft = data.BurstCount - 1;

            // 소비 탄약 = 발사 탄환 수. 탄약이 모자라면 남은 만큼만 쏜다 (Weapons §4).
            int fullShots = data.ProjectilesPerShot;
            int shots = Mathf.Min(fullShots, currentAmmo);
            currentAmmo -= shots;
            if (burstShotsLeft > 0 && currentAmmo > 0)
                nextReadyTime = now + data.BurstInterval;
            else
            {
                burstShotsLeft = 0;
                nextReadyTime = now + EffectiveCooldown;
            }
            if (data.ShootClip != null) SoundPlayer.Play(data.ShootClip);

            // 모자란 사격은 온전한 사격의 부채꼴 자리 중 중앙에 가까운 연속 구간만 채운다.
            // 구간을 정중앙에 둘 수 없으면(빈 자리가 홀수) 좌우 어느 쪽으로 치우칠지 매번 무작위
            // → 산탄총 3자리 중 2발: 중앙 + 좌/우 한쪽, 1발: 중앙.
            float step = fullShots > 1 ? data.SpreadAngleDegrees / (fullShots - 1) : 0f;
            int emptySlots = fullShots - shots;
            int firstSlot = emptySlots / 2;
            if (emptySlots % 2 == 1 && Random.value < 0.5f) firstSlot++;
            float baseAngle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;

            for (int i = 0; i < shots; i++)
            {
                float angle = baseAngle + step * (firstSlot + i - (fullShots - 1) * 0.5f);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                var bullet = Object.Instantiate(data.BulletPrefab, muzzlePos, Quaternion.identity);
                var bulletCol = bullet.GetComponent<Collider2D>();
                if (bulletCol == null)
                    throw new System.Exception($"[PickShoot] '{data.DisplayName}' bulletPrefab에 Collider2D 없음.");
                if (shooterCollider != null)
                    Physics2D.IgnoreCollision(shooterCollider, bulletCol, true);

                bullet.Launch(dir * data.BulletSpeed, this, damage, data.KnockbackForce, BouncesWith(data, mod), PenetrationWith(data, mod), ExplosionRadiusWith(data, mod), mod);
            }
        }

        // 회수 시 이 무기의 탄창에 1발 추가. 이미 가득 찼으면 false.
        public bool TryAddAmmo()
        {
            if (currentAmmo >= MaxAmmo) return false;
            currentAmmo++;
            return true;
        }

        // 개조를 붙인다 (Mods §1). 자루당 하나라 이미 붙은 자루에는 붙일 수 없다 — 호출자(개조 카드 후보·디버그 버튼)가 Mod를 먼저 확인해야 한다.
        // 이미 쏜 탄환은 발사 때의 개조 그대로 난다 (Bullet.Launch).
        public void AttachMod(ModData newMod)
        {
            if (newMod == null)
                throw new System.Exception($"[PickShoot] Weapon.AttachMod: '{data.DisplayName}'에 붙일 개조가 null.");
            if (mod != null)
                throw new System.Exception($"[PickShoot] Weapon.AttachMod: '{data.DisplayName}'에 이미 개조 '{mod.DisplayName}'가 붙어 있음 (자루당 하나, Mods §1). 호출자가 Mod를 먼저 확인해야 함.");
            mod = newMod;
        }

        // 자루 단위 레벨업 (GDD §7.3). 최대 탄약 상승분만큼 현재 탄약도 함께 충전한다 — 탄창 가산이 0인 고정 탄창은 오르지 않으므로 충전도 없다.
        // 호출자가 IsMaxLevel을 먼저 검증해야 한다 (레벨업 카드 후보·디버그 버튼).
        public void LevelUp()
        {
            if (IsMaxLevel)
                throw new System.Exception($"[PickShoot] Weapon.LevelUp: '{data.DisplayName}'는 이미 최대 레벨({MaxLevel}). 호출자가 IsMaxLevel을 먼저 검증해야 함.");
            int maxAmmoBefore = MaxAmmo;
            level++;
            currentAmmo += MaxAmmo - maxAmmoBefore;
        }
    }
}
