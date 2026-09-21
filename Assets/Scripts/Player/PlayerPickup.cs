using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
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

        readonly Collider2D[] hits = new Collider2D[16];
        // 자석 반경에 한 번 들어온 탄환. 반경을 벗어나도 회수될 때까지 계속 끌려온다.
        readonly List<Bullet> magnetized = new List<Bullet>();

        // 웨이브 클리어 sweep 중. 필드의 모든 탄환을 자석에 걸고 확정 속도로 끌어온다.
        bool sweeping;

        void FixedUpdate()
        {
            if (sweeping) MagnetizeAllBullets();
            else MagnetizeBulletsInRange();

            float span = Mathf.Max(0.0001f, magnetRadius - pickupRadius);
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
                float t = Mathf.Clamp01((magnetRadius - d) / span);
                bullet.MagnetPull = Mathf.Min(1f, Mathf.Max(t, bullet.MagnetPull + Time.fixedDeltaTime / magnetRampTime));

                Vector2 targetVel = dir * magnetTargetSpeed * bullet.MagnetPull;
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVel, alpha);
            }
        }

        void MagnetizeBulletsInRange()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, magnetRadius, hits);
            for (int i = 0; i < count; i++)
            {
                var bullet = hits[i].GetComponent<Bullet>();
                if (bullet == null || !bullet.IsSettled || bullet.IsMagnetized) continue;

                bullet.Magnetize();
                magnetized.Add(bullet);
            }
        }

        void PlayPickupFeedback(Vector3 position)
        {
            if (pickupEffectPrefab != null)
                Instantiate(pickupEffectPrefab, position, Quaternion.identity);
            if (pickupClip != null) SoundPlayer.Play(pickupClip);
        }

        // sweep 중에는 거리·페이즈와 무관하게 전부 건다. 그사이 새로 발사된 탄환도 다음 스텝에 걸린다.
        void MagnetizeAllBullets()
        {
            foreach (var bullet in FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            {
                if (bullet.IsMagnetized) continue;

                bullet.ForceSettle();
                bullet.Magnetize();
                magnetized.Add(bullet);
            }
        }

        // 웨이브 클리어 연출: 필드의 모든 탄환을 강제 정착 → 강한 자석으로 플레이어에게 끌어와 회수.
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
