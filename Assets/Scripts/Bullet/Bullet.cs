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
        [SerializeField] float wallBounceMinSpeed = 7f;
        [SerializeField, Range(0f, 89f)] float maxBounceAngleFromNormal = 75f;
        [SerializeField] float bounceFlashDuration = 0.09f;
        [SerializeField] Color bounceFlashColor = Color.white;
        [SerializeField] float bounceScalePulse = 1.5f;
        [SerializeField] float separationRadius = 0.6f;
        [SerializeField] float separationStrength = 3f;
        [SerializeField] LayerMask bulletLayerMask;

        Rigidbody2D rb;
        SpriteRenderer sr;
        Vector2 lastVelocity;
        Vector3 baseScale;
        Color baseColor;
        float slowTimer;
        float pickableAt;
        float flashUntil;
        bool flashing;
        bool settled;
        readonly Collider2D[] neighbors = new Collider2D[8];

        public bool IsSettled => settled;
        public bool IsPickable => Time.time >= pickableAt;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            baseScale = transform.localScale;
        }

        public void Launch(Vector2 velocity)
        {
            settled = false;
            slowTimer = 0f;
            pickableAt = Time.time + pickupDelay;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = velocity;
        }

        void Update()
        {
            if (!flashing) return;
            if (Time.time < flashUntil) return;

            if (sr != null && !settled) sr.color = baseColor;
            transform.localScale = baseScale;
            flashing = false;
        }

        void FixedUpdate()
        {
            Vector2 separation = ComputeSeparation();

            if (settled)
            {
                if (separation.sqrMagnitude > 0.0001f)
                    rb.MovePosition(rb.position + separation * Time.fixedDeltaTime);
                return;
            }

            if (separation.sqrMagnitude > 0.0001f)
                rb.linearVelocity += separation * Time.fixedDeltaTime;

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

        Vector2 ComputeSeparation()
        {
            if (separationStrength <= 0f || separationRadius <= 0f) return Vector2.zero;

            int count = Physics2D.OverlapCircleNonAlloc(rb.position, separationRadius, neighbors, bulletLayerMask);
            Vector2 push = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var c = neighbors[i];
                if (c == null || c.attachedRigidbody == rb) continue;
                Vector2 diff = rb.position - (Vector2)c.transform.position;
                float d = diff.magnitude;
                if (d < 0.0001f) continue;
                float weight = 1f - Mathf.Clamp01(d / separationRadius);
                push += diff.normalized * weight;
            }
            return push * separationStrength;
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            var enemy = col.collider.GetComponent<EnemyHealth>();
            if (enemy == null)
            {
                HandleWallBounce(col);
                return;
            }

            enemy.TakeDamage(damage);

            var movement = col.collider.GetComponent<EnemyMovement>();
            if (movement != null && lastVelocity.sqrMagnitude > 0.01f)
                movement.ApplyKnockback(lastVelocity.normalized * knockbackForce);

            rb.linearVelocity *= postHitSpeedMultiplier;
        }

        void HandleWallBounce(Collision2D col)
        {
            Vector2 avgNormal = AverageContactNormal(col);
            if (avgNormal.sqrMagnitude < 0.0001f) return;
            avgNormal.Normalize();

            Vector2 incoming = lastVelocity;
            float incomingSpeed = incoming.magnitude;
            if (incomingSpeed < 0.01f) return;

            Vector2 reflected = Vector2.Reflect(incoming, avgNormal);
            Vector2 clampedDir = ClampDirectionToNormalCone(reflected.normalized, avgNormal);
            float outSpeed = Mathf.Max(reflected.magnitude, wallBounceMinSpeed);

            Vector2 preOverrideVel = rb.linearVelocity;
            rb.linearVelocity = clampedDir * outSpeed;

            bool boosted = preOverrideVel.magnitude < wallBounceMinSpeed
                || Vector2.Dot(preOverrideVel.normalized, clampedDir) < 0.98f;
            if (boosted) TriggerBounceEffect();
        }

        Vector2 AverageContactNormal(Collision2D col)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < col.contactCount; i++)
                sum += col.GetContact(i).normal;
            return sum;
        }

        Vector2 ClampDirectionToNormalCone(Vector2 dir, Vector2 normal)
        {
            float dot = Vector2.Dot(dir, normal);
            if (dot <= 0f) return normal;

            float minDot = Mathf.Cos(maxBounceAngleFromNormal * Mathf.Deg2Rad);
            if (dot >= minDot) return dir;

            Vector2 tangent = dir - normal * dot;
            float tanLen = tangent.magnitude;
            if (tanLen < 0.0001f) return normal;
            Vector2 tanDir = tangent / tanLen;
            return normal * minDot + tanDir * Mathf.Sqrt(1f - minDot * minDot);
        }

        void TriggerBounceEffect()
        {
            if (settled) return;
            if (sr != null) sr.color = bounceFlashColor;
            transform.localScale = baseScale * bounceScalePulse;
            flashUntil = Time.time + bounceFlashDuration;
            flashing = true;
        }

        void Settle()
        {
            settled = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            flashing = false;
            transform.localScale = baseScale;

            if (sr != null) sr.color = new Color(1f, 0.5f, 0.1f);
        }
    }
}
