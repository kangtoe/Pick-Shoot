using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] float activeSpeedThreshold = 2f;
        [SerializeField] float interactionDelay = 0.4f;
        [SerializeField] float sustainDuration = 0.5f;
        [SerializeField] float decayLinearDamping = 5f;
        [SerializeField, Range(0f, 1f)] float postHitSpeedMultiplier = 0.6f;
        [SerializeField] float wallBounceMinSpeed = 7f;
        [SerializeField, Range(0f, 89f)] float maxBounceAngleFromNormal = 75f;
        [SerializeField] float bounceFlashDuration = 0.09f;
        [SerializeField] Color bounceFlashColor = Color.white;
        [SerializeField] float bounceScalePulse = 1.5f;
        [SerializeField] float separationRadius = 0.6f;
        [SerializeField] float separationStrength = 3f;
        [SerializeField] LayerMask bulletLayerMask;
        [SerializeField] GameObject hitEffectPrefab;

        const string ActiveLayerName = "Bullet";
        const string InactiveLayerName = "BulletInactive";

        Rigidbody2D rb;
        SpriteRenderer sr;
        Vector2 lastVelocity;
        Vector3 baseScale;
        Color baseColor;
        float interactableAt;
        float sustainEndAt;
        bool sustaining;
        float flashUntil;
        bool flashing;
        int activeLayerIdx;
        int inactiveLayerIdx;
        bool wasActive;
        Weapon ownerWeapon;
        int damage;
        float knockbackForce;
        readonly Collider2D[] neighbors = new Collider2D[8];

        public bool IsInteractable => Time.time >= interactableAt;
        public bool IsActive => lastVelocity.sqrMagnitude >= activeSpeedThreshold * activeSpeedThreshold;
        public Weapon OwnerWeapon => ownerWeapon;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            baseScale = transform.localScale;

            activeLayerIdx = LayerMask.NameToLayer(ActiveLayerName);
            inactiveLayerIdx = LayerMask.NameToLayer(InactiveLayerName);
            if (activeLayerIdx < 0 || inactiveLayerIdx < 0)
                throw new System.Exception($"[PickShoot] 레이어 미설정: '{ActiveLayerName}' / '{InactiveLayerName}'. Project Settings > Tags and Layers에서 추가 필요.");
        }

        public void Launch(Vector2 velocity, Weapon owner, int damage, float knockbackForce)
        {
            if (owner == null)
                throw new System.Exception("[PickShoot] Bullet.Launch: owner Weapon이 null. 탄환은 반드시 무기에 귀속되어야 회수 시 탄창 충전이 가능합니다.");
            ownerWeapon = owner;
            this.damage = damage;
            this.knockbackForce = knockbackForce;

            interactableAt = Time.time + interactionDelay;
            sustainEndAt = Time.time + sustainDuration;
            sustaining = true;
            rb.linearDamping = 0f;
            rb.linearVelocity = velocity;
            lastVelocity = velocity;
            gameObject.layer = activeLayerIdx;
            wasActive = true;
        }

        void Update()
        {
            if (!flashing) return;
            if (Time.time < flashUntil) return;

            if (sr != null) sr.color = baseColor;
            transform.localScale = baseScale;
            flashing = false;
        }

        void FixedUpdate()
        {
            if (sustaining && Time.time >= sustainEndAt)
            {
                rb.linearDamping = decayLinearDamping;
                sustaining = false;
            }

            Vector2 separation = ComputeSeparation();
            if (separation.sqrMagnitude > 0.0001f)
                rb.linearVelocity += separation * Time.fixedDeltaTime;

            lastVelocity = rb.linearVelocity;

            bool nowActive = IsActive;
            if (nowActive != wasActive)
            {
                gameObject.layer = nowActive ? activeLayerIdx : inactiveLayerIdx;
                wasActive = nowActive;
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

            if (!IsActive) return;

            enemy.TakeDamage(damage);

            var movement = col.collider.GetComponent<EnemyMovement>();
            if (movement != null && lastVelocity.sqrMagnitude > 0.01f)
                movement.ApplyKnockback(lastVelocity.normalized * knockbackForce);

            if (hitEffectPrefab != null)
            {
                Vector3 hitPos = col.contactCount > 0 ? (Vector3)col.GetContact(0).point : transform.position;
                Instantiate(hitEffectPrefab, hitPos, Quaternion.identity);
            }

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
            if (sr != null) sr.color = bounceFlashColor;
            transform.localScale = baseScale * bounceScalePulse;
            flashUntil = Time.time + bounceFlashDuration;
            flashing = true;
        }
    }
}
