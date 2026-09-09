using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 2.5f;
        [SerializeField] float knockbackDamping = 20f;
        [SerializeField] float separationRadius = 0.9f;
        [SerializeField] float separationStrength = 4f;
        [SerializeField] LayerMask enemyLayerMask;

        Rigidbody2D rb;
        Transform target;
        Vector2 knockback;
        readonly Collider2D[] neighbors = new Collider2D[8];

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        public void ApplyKnockback(Vector2 velocity)
        {
            knockback = velocity;
        }

        void FixedUpdate()
        {
            if (knockback.sqrMagnitude > 0.0001f)
                knockback = Vector2.MoveTowards(knockback, Vector2.zero, knockbackDamping * Time.fixedDeltaTime);
            else
                knockback = Vector2.zero;

            Vector2 pursue = Vector2.zero;
            if (target != null)
            {
                Vector2 delta = (Vector2)target.position - rb.position;
                if (delta.sqrMagnitude > 0.0001f)
                    pursue = delta.normalized * moveSpeed;
            }

            Vector2 separation = ComputeSeparation();

            Vector2 step = (pursue + separation + knockback) * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + step);
        }

        Vector2 ComputeSeparation()
        {
            if (separationStrength <= 0f || separationRadius <= 0f) return Vector2.zero;

            int count = Physics2D.OverlapCircleNonAlloc(rb.position, separationRadius, neighbors, enemyLayerMask);
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
    }
}
