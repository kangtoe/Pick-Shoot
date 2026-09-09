using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] float settleSpeedThreshold = 0.4f;
        [SerializeField] float settleTime = 0.2f;
        [SerializeField] float pickupDelay = 0.2f;

        Rigidbody2D rb;
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
