using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] float settleSpeedThreshold = 0.4f;
        [SerializeField] float settleTime = 0.2f;
        [SerializeField] float pickupDelay = 0.2f;
        [SerializeField] int damage = 1;
        [SerializeField] float knockbackForce = 6f;
        [SerializeField, Range(0f, 1f)] float postHitSpeedMultiplier = 0.6f;

        Rigidbody2D rb;
        Vector2 lastVelocity;
        float slowTimer;
        float pickableAt;
        bool settled;

        public bool IsSettled => settled;
        public bool IsPickable => Time.time >= pickableAt;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        public void Launch(Vector2 velocity)
        {
            settled = false;
            slowTimer = 0f;
            pickableAt = Time.time + pickupDelay;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = velocity;
        }

        void FixedUpdate()
        {
            if (settled) return;

            lastVelocity = rb.linearVelocity;

            float sqr = rb.linearVelocity.sqrMagnitude;
            if (sqr < settleSpeedThreshold * settleSpeedThreshold)
            {
                slowTimer += Time.fixedDeltaTime;
                if (slowTimer >= settleTime) Settle();
            }
            else
            {
                slowTimer = 0f;
            }
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            var enemy = col.collider.GetComponent<EnemyHealth>();
            if (enemy == null) return;

            enemy.TakeDamage(damage);

            var movement = col.collider.GetComponent<EnemyMovement>();
            if (movement != null && lastVelocity.sqrMagnitude > 0.01f)
                movement.ApplyKnockback(lastVelocity.normalized * knockbackForce);

            rb.linearVelocity *= postHitSpeedMultiplier;
        }

        void Settle()
        {
            settled = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 0.5f, 0.1f);
        }
    }
}
