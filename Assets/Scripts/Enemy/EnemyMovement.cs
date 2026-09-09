using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 2.5f;
        [SerializeField] float knockbackDamping = 20f;

        Rigidbody2D rb;
        Transform target;
        Vector2 knockback;

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

            Vector2 step = (pursue + knockback) * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + step);
        }
    }
}
