using UnityEngine;

namespace PickShoot
{
    // 적 이동의 공통 골격. 파생 클래스가 매 물리 스텝의 목표 속도(Steer)를 정하고, 여기서는 그 위에 적끼리의 분리 힘과
    // 탄환 넉백을 얹어 MovePosition으로 옮긴다. 넉백은 ApplyKnockback으로 받은 속도가 knockbackDamping 비율로 줄어드는 방식이다.
    // 스프라이트는 회전하지 않고 좌우 반전으로 방향을 보인다 (시트의 적은 플레이어·무기와 같이 오른쪽을 보고 그려져 있다).
    // Face(dx)는 x 차이가 데드존보다 작으면 마지막 방향을 유지해 반전이 매 프레임 뒤집히지 않게 한다.
    // 몸은 Kinematic이라 정적 콜라이더(벽)와는 접촉 이벤트도 밀어내기도 없다 — 그래서 옮기기 전에 이동량을 가로·세로로 나눠 벽 레이어에
    // 캐스트하고, 막힌 축은 벽 앞까지만 나아간다 (Enemies §1). 막히지 않은 축은 그대로 가므로 벽을 따라 미끄러지고, 구석에서는 멈춘다.
    // 막힌 스텝에는 OnWallBlocked를 부른다 — 벽에 닿는 것이 곧 행동의 끝인 적(돌진)이 받아 쓴다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class EnemyMover : MonoBehaviour
    {
        [SerializeField] EnemyData data;
        [SerializeField] float knockbackDamping = 20f;
        [SerializeField] float separationRadius = 0.9f;
        [SerializeField] float separationStrength = 4f;
        [SerializeField] LayerMask enemyLayerMask;
        [SerializeField, Min(0f)] float facingDeadZone = 0.1f; // x 차이가 이보다 작으면 방향 유지
        [SerializeField] LayerMask wallLayerMask; // 넘지 못하는 벽

        // 벽 앞에서 멈출 때 남기는 틈 (월드 유닛, 1텍셀 미만). 벽에 닿은 채로 두면 벽을 따라 미끄러지는 캐스트가 그 벽에 걸린다.
        const float WallSkin = 0.05f;

        Vector2 knockback;
        readonly Collider2D[] neighbors = new Collider2D[8];
        ContactFilter2D wallFilter;
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];

        protected EnemyData Data => data;
        protected Rigidbody2D Body { get; private set; }
        protected SpriteRenderer Sprite { get; private set; }
        // 플레이어. 사망해 파괴되면 null이 된다.
        protected Transform Target { get; private set; }

        // 이 스텝에 분리 힘을 받을지. 궤도가 휘면 안 되는 이동(돌진)은 끈다.
        protected virtual bool UseSeparation => true;

        protected virtual void Awake()
        {
            if (data == null)
                throw new System.Exception($"[PickShoot] {GetType().Name}.data 미설정 ({name}).");
            if (wallLayerMask.value == 0)
                throw new System.Exception($"[PickShoot] {GetType().Name}.wallLayerMask 미설정 ({name}). 넘지 못할 벽 레이어(Wall) 필요.");
            Body = GetComponent<Rigidbody2D>();
            Sprite = GetComponent<SpriteRenderer>();
            wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(wallLayerMask);
            wallFilter.useTriggers = false;
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) Target = player.transform;
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

            // Steer가 상태를 바꿀 수 있으므로 분리 여부는 그 전에 읽는다.
            bool separate = UseSeparation;
            Vector2 velocity = Steer() + knockback;
            if (separate) velocity += ComputeSeparation();

            Vector2 move = velocity * Time.fixedDeltaTime;
            bool blockedX = ClampToWall(ref move.x, Vector2.right);
            bool blockedY = ClampToWall(ref move.y, Vector2.up);
            Body.MovePosition(Body.position + move);
            if (blockedX || blockedY) OnWallBlocked();
        }

        // 이 물리 스텝의 목표 속도. 방향 전환(Face)도 여기서 한다.
        protected abstract Vector2 Steer();

        // 이 스텝의 이동이 벽에 막혔다. 넉백·분리 힘에 밀려 막힌 것도 포함한다.
        protected virtual void OnWallBlocked() { }

        // 한 축의 이동량을 벽 앞(WallSkin만큼 못 미친 곳)까지로 줄인다. 줄였으면 true.
        bool ClampToWall(ref float amount, Vector2 axis)
        {
            float distance = Mathf.Abs(amount);
            if (distance < 0.00001f) return false;

            Vector2 direction = amount > 0f ? axis : -axis;
            float allowed = distance;
            int count = Body.Cast(direction, wallFilter, wallHits, distance + WallSkin);
            for (int i = 0; i < count; i++)
                allowed = Mathf.Min(allowed, Mathf.Max(0f, wallHits[i].distance - WallSkin));
            if (allowed >= distance) return false;

            amount = Mathf.Sign(amount) * allowed;
            return true;
        }

        protected void Face(float dx)
        {
            if (Mathf.Abs(dx) < facingDeadZone) return;
            Sprite.flipX = dx < 0f;
        }

        Vector2 ComputeSeparation()
        {
            if (separationStrength <= 0f || separationRadius <= 0f) return Vector2.zero;

            int count = Physics2D.OverlapCircleNonAlloc(Body.position, separationRadius, neighbors, enemyLayerMask);
            Vector2 push = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var c = neighbors[i];
                if (c == null || c.attachedRigidbody == Body) continue;
                Vector2 diff = Body.position - (Vector2)c.transform.position;
                float d = diff.magnitude;
                if (d < 0.0001f) continue;
                float weight = 1f - Mathf.Clamp01(d / separationRadius);
                push += diff.normalized * weight;
            }
            return push * separationStrength;
        }
    }
}
