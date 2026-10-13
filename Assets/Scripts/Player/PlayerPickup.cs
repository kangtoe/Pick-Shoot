using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 탄환 회수 (GDD §3.2): 자석 반경에 들어온 탄환을 끌어와 pickupRadius 안에서 귀속 무기의 탄창에 넣는다.
    // 자석 반경과 끌림 속도는 PlayerStats의 배율(유물의 회수 범위·회수 속도, Relics §2)을 받는다. 흡수 거리(pickupRadius)와 sweep 속도는 그대로.
    // 회수 펄스 능력(Characters §2)은 반경 안의 탄환을 한 번에 자석에 건다 (Pulse) — 걸린 뒤는 평소 자석과 같다.
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerPickup : MonoBehaviour
    {
        [SerializeField] float pickupRadius = 0.9f;
        [SerializeField] float magnetRadius = 1.8f;
        [SerializeField] float magnetTargetSpeed = 18f;
        [SerializeField] float magnetBlendRate = 12f;
        // 자석에 걸린 탄환의 끌림이 거리와 무관하게 최대까지 차오르는 데 걸리는 시간.
        [SerializeField, Min(0.01f)] float magnetRampTime = 0.5f;
        [SerializeField] float sweepSpeed = 30f;
        [SerializeField] GameObject pickupEffectPrefab;
        [SerializeField] AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] float pickupVolume = 0.55f;

        readonly Collider2D[] hits = new Collider2D[16];
        // 자석 반경에 한 번 들어온 탄환. 반경을 벗어나도 회수될 때까지 계속 끌려온다.
        readonly List<Bullet> magnetized = new List<Bullet>();
        // 발사 직후 아직 자석 반경을 벗어나지 않은 튕김 페이즈 탄환 (GDD §3.2). 총구가 반경 안이라 그대로 두면 쏘는 즉시 되돌아오므로,
        // 반경을 한 번 벗어나거나 그 안에서 정착하기 전에는 걸지 않는다.
        readonly List<Bullet> departing = new List<Bullet>();

        // 웨이브 클리어 sweep 중. 필드의 모든 탄환을 자석에 걸고 확정 속도로 끌어온다.
        bool sweeping;
        PlayerStats stats;

        // 유물 배율을 받은 자석 반경·끌림 속도 (Relics §2). 발사 직후 "반경을 벗어났는가"(ReleaseDeparted)도 같은 반경으로 잰다.
        float MagnetRadius => magnetRadius * stats.PickupRadiusMultiplier;
        float MagnetTargetSpeed => magnetTargetSpeed * stats.PickupSpeedMultiplier;
        // 배율을 곱하기 전의 자산 값. 보상 화면의 상세 패널이 유물의 효과를 실제 값으로 보여줄 때 쓴다.
        public float BaseMagnetRadius => magnetRadius;
        public float BaseMagnetSpeed => magnetTargetSpeed;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            if (stats == null)
                throw new System.Exception("[PickShoot] PlayerPickup: 플레이어에 PlayerStats 없음 (회수 범위·속도 배율 출처).");
        }

        void OnEnable()
        {
            Bullet.AutoRetrieveSettled += OnAutoRetrieveSettled;
            Bullet.Launched += OnLaunched;
        }

        void OnDisable()
        {
            Bullet.AutoRetrieveSettled -= OnAutoRetrieveSettled;
            Bullet.Launched -= OnLaunched;
        }

        void OnLaunched(Bullet bullet) => departing.Add(bullet);

        // 정착 시 자동 회수하는 무기(Weapons §9)의 탄환. 거리와 무관하게 바로 자석에 건다 — 끌어오기·회수는 FixedUpdate의 자석 경로가 맡는다.
        // 끌림은 반경 밖에서도 시간에 따라 최대까지 차오르므로 아레나 반대편에서도 돌아온다.
        void OnAutoRetrieveSettled(Bullet bullet)
        {
            if (bullet.IsMagnetized)
                throw new System.Exception($"[PickShoot] PlayerPickup: 자동 회수 알림을 받은 탄환 '{bullet.name}'이 이미 자석에 걸려 있음. Bullet이 정착을 두 번 알림.");
            bullet.Magnetize();
            magnetized.Add(bullet);
        }

        void FixedUpdate()
        {
            if (sweeping) MagnetizeAllBullets();
            else MagnetizeBulletsInRange();
            // 거는 쪽(OverlapCircle)을 먼저 — 벗어남 판정과 겹침 판정이 경계에서 어긋나도 같은 스텝에 풀리자마자 다시 걸리지 않게.
            ReleaseDeparted();

            float magnetRadiusNow = MagnetRadius;
            float span = Mathf.Max(0.0001f, magnetRadiusNow - pickupRadius);
            float alpha = 1f - Mathf.Exp(-magnetBlendRate * Time.fixedDeltaTime);
            for (int i = magnetized.Count - 1; i >= 0; i--)
            {
                var bullet = magnetized[i];
                Vector2 toPlayer = (Vector2)transform.position - (Vector2)bullet.transform.position;
                float d = toPlayer.magnitude;

                if (d <= pickupRadius)
                {
                    var owner = bullet.OwnerWeapon;
                    if (owner == null)
                        throw new System.Exception("[PickShoot] 회수 대상 Bullet에 OwnerWeapon이 null. 발사 경로에서 귀속이 누락되었습니다.");
                    // 탄창이 가득 차 넣을 수 없는 탄환은 곁에 남긴다. sweep은 그런 탄환도 거둬 필드를 비운다.
                    if (!owner.TryAddAmmo() && !sweeping) continue;
                    PlayPickupFeedback(bullet.transform.position);
                    Destroy(bullet.gameObject);
                    magnetized.RemoveAt(i);
                    continue;
                }

                var rb = bullet.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                Vector2 dir = toPlayer / Mathf.Max(0.0001f, d);
                if (sweeping)
                {
                    // 감속을 무시하고 확정 속도로 끌어당김. Bullet의 damping은 그대로 두되 매 스텝 덮어씀.
                    // sweep이 끝나도 남은 탄환은 최대 세기의 자석으로 이어서 끌려온다.
                    bullet.MagnetPull = 1f;
                    rb.linearVelocity = dir * sweepSpeed;
                    continue;
                }

                // 끌림은 가까울수록 세고, 한 번 오른 세기는 멀어져도 줄지 않는다. 반경 밖에서도 시간에 따라
                // 최대까지 차오르므로 스치고 지나간 탄환도 달아나는 플레이어를 끝내 따라잡는다.
                float t = Mathf.Clamp01((magnetRadiusNow - d) / span);
                bullet.MagnetPull = Mathf.Min(1f, Mathf.Max(t, bullet.MagnetPull + Time.fixedDeltaTime / magnetRampTime));

                Vector2 targetVel = dir * MagnetTargetSpeed * bullet.MagnetPull;
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVel, alpha);
            }
        }

        void MagnetizeBulletsInRange()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, MagnetRadius, hits);
            for (int i = 0; i < count; i++)
            {
                var bullet = hits[i].GetComponent<Bullet>();
                if (bullet == null || bullet.IsMagnetized) continue;
                // 튕김 페이즈라도 반경에 들어오면 건다 (GDD §3.2) — 발사 직후 아직 반경을 벗어나지 않은 탄환만 빼고.
                if (!bullet.IsSettled && departing.Contains(bullet)) continue;

                bullet.Magnetize();
                magnetized.Add(bullet);
            }
        }

        // 발사 직후 명단에서 자석 반경을 벗어났거나 그 안에서 정착한 탄환을 뺀다. 벗어남은 거는 쪽(OverlapCircle)과 같은 기준인
        // 콜라이더 가장자리로 잰다 — 중심 거리로 재면 가장자리가 아직 걸쳐 있는 스텝에 풀려 바로 다시 걸린다.
        void ReleaseDeparted()
        {
            Vector2 center = transform.position;
            float magnetRadiusNow = MagnetRadius;
            for (int i = departing.Count - 1; i >= 0; i--)
            {
                var bullet = departing[i];
                if (bullet == null || bullet.IsSettled)
                {
                    departing.RemoveAt(i);
                    continue;
                }
                Vector2 closest = Physics2D.ClosestPoint(center, bullet.GetComponent<Collider2D>());
                if ((closest - center).magnitude > magnetRadiusNow) departing.RemoveAt(i);
            }
        }

        void PlayPickupFeedback(Vector3 position)
        {
            if (pickupEffectPrefab != null)
                Instantiate(pickupEffectPrefab, position, Quaternion.identity);
            if (pickupClip != null) SoundPlayer.Play(pickupClip, pickupVolume);
        }

        // 회수 펄스 (Characters §2): 플레이어 중심 radius 안의 탄환을 페이즈·발사 직후 여부와 무관하게 전부 건다 — 웨이브 클리어 sweep의 거는 일을 반경 안으로
        // 좁힌 것이다. 날아가는 탄환은 그 자리에서 튕김 페이즈가 끝나 남은 튕김·관통을 버리고(Bullet.Magnetize), 수집형이 끌어 둔 탄환은 자석에 걸리는 순간
        // 수집형이 손을 뗀다. 끌림은 처음부터 최대다 — 평소처럼 0에서 차오르면 먼 탄환이 머뭇거리다 와서 빨아들이는 느낌이 나지 않는다. 끌어오기·회수는
        // FixedUpdate의 평소 자석 경로가 맡는다.
        // 반경은 탄환 중심까지의 거리로 잰다. 이미 걸린 탄환은 콜라이더가 꺼져 있어도 상관없도록 물리 조회 대신 모든 탄환을 훑는다 (sweep과 같다).
        public void Pulse(float radius)
        {
            if (radius <= 0f)
                throw new System.Exception($"[PickShoot] PlayerPickup.Pulse: 반경은 양수여야 함 ({radius}).");
            Vector2 center = transform.position;
            float radiusSqr = radius * radius;
            foreach (var bullet in FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            {
                if (bullet.IsMagnetized) continue;
                if (((Vector2)bullet.transform.position - center).sqrMagnitude > radiusSqr) continue;

                bullet.Magnetize();
                bullet.MagnetPull = 1f;
                magnetized.Add(bullet);
            }
        }

        // sweep 중에는 거리·페이즈·발사 직후 여부와 무관하게 전부 건다. 그사이 새로 발사된 탄환도 다음 스텝에 걸린다.
        void MagnetizeAllBullets()
        {
            foreach (var bullet in FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            {
                if (bullet.IsMagnetized) continue;

                bullet.Magnetize();
                magnetized.Add(bullet);
            }
        }

        // 웨이브 클리어 연출: 필드의 모든 탄환을 자석에 걸어(튕김 중이면 거기서 끝내고) 강한 자석으로 플레이어에게 끌어와 회수.
        // 끌어오기·회수는 FixedUpdate의 자석 경로가 맡는다 (회수 경로가 둘이면 같은 탄환을 두 번 셀 수 있다).
        // 회수가 일찍 끝나도 duration을 끝까지 채운다 — 호출부가 이 시간을 클리어 후 대기로 쓴다.
        // 회수 미완료 탄환은 자석에 걸린 채 남아 이후에도 계속 끌려온다.
        public IEnumerator SweepAll(float duration)
        {
            sweeping = true;
            yield return new WaitForSeconds(duration);
            sweeping = false;
        }
    }
}
