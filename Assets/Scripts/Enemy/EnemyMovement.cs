using UnityEngine;

namespace PickShoot
{
    // 플레이어 추적 이동. 스프라이트는 회전하지 않고 플레이어가 왼쪽에 있으면 좌우 반전해 항상 플레이어를 향한다
    // (시트의 적은 플레이어·무기와 같이 오른쪽을 보고 그려져 있다). 플레이어와 x가 거의 같을 때는
    // 반전이 매 프레임 뒤집히지 않도록 데드존 안에서는 마지막 방향을 유지한다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyMovement : MonoBehaviour
    {
        [SerializeField] EnemyData data;
        [SerializeField] float knockbackDamping = 20f;
        [SerializeField] float separationRadius = 0.9f;
        [SerializeField] float separationStrength = 4f;
        [SerializeField] LayerMask enemyLayerMask;
        [SerializeField, Min(0f)] float facingDeadZone = 0.1f; // 플레이어와의 x 차이가 이보다 작으면 방향 유지

        Rigidbody2D rb;
        SpriteRenderer sr;
        Transform target;
        Vector2 knockback;
        float moveSpeed;
        readonly Collider2D[] neighbors = new Collider2D[8];

        void Awake()
        {
            if (data == null)
                throw new System.Exception($"[PickShoot] EnemyMovement.data 미설정 ({name}).");
            moveSpeed = data.MoveSpeed;
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
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
                FacePlayer(delta.x);
            }

            Vector2 separation = ComputeSeparation();

            Vector2 step = (pursue + separation + knockback) * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + step);
        }

        void FacePlayer(float dx)
        {
            if (Mathf.Abs(dx) < facingDeadZone) return;
            sr.flipX = dx < 0f;
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
