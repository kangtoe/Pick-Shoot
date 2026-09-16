using System.Collections;
using UnityEngine;

namespace PickShoot
{
    public class PlayerPickup : MonoBehaviour
    {
        [SerializeField] float pickupRadius = 0.9f;
        [SerializeField] float magnetRadius = 2.8f;
        [SerializeField] float magnetTargetSpeed = 18f;
        [SerializeField] float magnetBlendRate = 12f;
        [SerializeField] float sweepSpeed = 30f;
        [SerializeField] GameObject pickupEffectPrefab;
        [SerializeField] AudioClip pickupClip;

        readonly Collider2D[] hits = new Collider2D[16];

        void FixedUpdate()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, magnetRadius, hits);
            for (int i = 0; i < count; i++)
            {
                var bullet = hits[i].GetComponent<Bullet>();
                if (bullet == null || !bullet.IsSettled) continue;

                Vector2 toPlayer = (Vector2)transform.position - (Vector2)bullet.transform.position;
                float d = toPlayer.magnitude;

                if (d <= pickupRadius)
                {
                    var owner = bullet.OwnerWeapon;
                    if (owner == null)
                        throw new System.Exception("[PickShoot] 회수 대상 Bullet에 OwnerWeapon이 null. 발사 경로에서 귀속이 누락되었습니다.");
                    if (!owner.TryAddAmmo()) return;
                    PlayPickupFeedback(bullet.transform.position);
                    Destroy(bullet.gameObject);
                    continue;
                }

                var rb = bullet.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                float span = Mathf.Max(0.0001f, magnetRadius - pickupRadius);
                float t = Mathf.Clamp01((magnetRadius - d) / span);
                Vector2 dir = toPlayer / Mathf.Max(0.0001f, d);
                Vector2 targetVel = dir * magnetTargetSpeed * t;
                float alpha = 1f - Mathf.Exp(-magnetBlendRate * Time.fixedDeltaTime);
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVel, alpha);
            }
        }

        void PlayPickupFeedback(Vector3 position)
        {
            if (pickupEffectPrefab != null)
                Instantiate(pickupEffectPrefab, position, Quaternion.identity);
            if (pickupClip != null) SoundPlayer.Play(pickupClip);
        }

        // 웨이브 클리어 연출: 필드의 모든 탄환을 강제 정착 → 강한 자석으로 플레이어에게 끌어와 회수.
        // duration 내에 회수가 끝나면 조기 종료. 회수 미완료 탄환은 그대로 두고 정착 상태 유지.
        public IEnumerator SweepAll(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                var bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
                if (bullets.Length == 0) yield break;

                Vector2 playerPos = transform.position;
                foreach (var b in bullets)
                {
                    if (b == null) continue;
                    b.ForceSettle();

                    Vector2 bulletPos = b.transform.position;
                    Vector2 toPlayer = playerPos - bulletPos;
                    float d = toPlayer.magnitude;

                    if (d <= pickupRadius)
                    {
                        var owner = b.OwnerWeapon;
                        if (owner == null)
                            throw new System.Exception("[PickShoot] Sweep 회수 대상 Bullet에 OwnerWeapon이 null.");
                        owner.TryAddAmmo();
                        PlayPickupFeedback(b.transform.position);
                        Destroy(b.gameObject);
                        continue;
                    }

                    var rb = b.GetComponent<Rigidbody2D>();
                    if (rb == null) continue;
                    Vector2 dir = toPlayer / Mathf.Max(0.0001f, d);
                    // 감속을 무시하고 확정 속도로 끌어당김. Bullet의 damping은 그대로 두되 매 프레임 덮어씀.
                    rb.linearVelocity = dir * sweepSpeed;
                }

                yield return null;
                elapsed += Time.deltaTime;
            }
        }
    }
}
