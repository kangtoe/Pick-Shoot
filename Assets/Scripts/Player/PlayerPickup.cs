using UnityEngine;

namespace PickShoot
{
    public class PlayerPickup : MonoBehaviour
    {
        [SerializeField] PlayerAmmo ammo;
        [SerializeField] float pickupRadius = 0.9f;
        [SerializeField] float magnetRadius = 2.8f;
        [SerializeField] float magnetTargetSpeed = 18f;
        [SerializeField] float magnetBlendRate = 12f;
        [SerializeField] GameObject pickupEffectPrefab;

        readonly Collider2D[] hits = new Collider2D[16];

        void Awake()
        {
            if (ammo == null)
                throw new System.Exception("[PickShoot] PlayerPickup.ammo 미할당.");
        }

        void FixedUpdate()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, magnetRadius, hits);
            for (int i = 0; i < count; i++)
            {
                var bullet = hits[i].GetComponent<Bullet>();
                if (bullet == null || !bullet.IsInteractable) continue;

                Vector2 toPlayer = (Vector2)transform.position - (Vector2)bullet.transform.position;
                float d = toPlayer.magnitude;

                if (d <= pickupRadius)
                {
                    if (!ammo.TryAdd()) return;
                    if (pickupEffectPrefab != null)
                        Instantiate(pickupEffectPrefab, bullet.transform.position, Quaternion.identity);
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
    }
}
