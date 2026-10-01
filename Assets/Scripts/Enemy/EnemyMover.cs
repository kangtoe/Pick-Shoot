using UnityEngine;

namespace PickShoot
{
    // 적 이동의 공통 골격. 파생 클래스가 매 물리 스텝의 목표 속도(Steer)를 정하고, 여기서는 그 위에 적끼리의 분리 힘과
    // 탄환 넉백을 얹어 MovePosition으로 옮긴다. 넉백은 ApplyKnockback으로 받은 속도가 knockbackDamping 비율로 줄어드는 방식이다.
    // 스프라이트는 회전하지 않고 좌우 반전으로 방향을 보인다 (시트의 적은 플레이어·무기와 같이 오른쪽을 보고 그려져 있다).
    // Face(dx)는 x 차이가 데드존보다 작으면 마지막 방향을 유지해 반전이 매 프레임 뒤집히지 않게 한다.
    // 몸은 Kinematic이라 정적 콜라이더(벽)와는 접촉 이벤트도 밀어내기도 없다 — 그래서 옮기기 전에 이동량을 가로·세로로 나눠 벽 레이어에
    // 캐스트하고, 막힌 축은 벽 앞까지만 나아간다 (Enemies §1). 막히지 않은 축은 그대로 가므로 벽을 따라 미끄러지고, 구석에서는 멈춘다.
    // 막힌 스텝에는 OnWallBlocked를 부른다 — 벽에 닿는 것이 곧 행동의 끝인 적(돌진)과 벽에서 방향을 꺾는 적(방랑 이동)이 받아 쓴다.
    // 옮겨 갈 자리가 정해지면 OnMoved를 부른다 — 몸에 딸린 것(서펜트의 몸통)을 함께 옮기는 적이 받아 쓴다.
    // 분리 힘의 이웃은 SeparatesFrom으로 거른다 — 기본으로 쏘는 졸개(EnemyDart)는 발사체처럼 누구와도 주고받지 않고, 한 몸처럼 움직이는 것(서펜트의 자기 사슬 마디)을
    // 더 빼는 적이 받아 쓴다.
    // 화면 가장자리 판정(ScreenEdgeSide)도 여기 둔다 — 방랑 이동은 거기서 경로를 바꾸고 쏘는 졸개는 죽는다. 카메라가 플레이어를 따라가므로 가만두면 화면 밖으로 사라진다.
    // 뚫을 수 없는 몸(IsSolid, 서펜트 마디)과 겹치면 분리 힘과 별개로 겹친 만큼 그 스텝에 바로 밀려난다 — 부드러운 분리 힘만으로는
    // 걸어오는 적이 쓸고 가는 몸통을 파고든다. 여럿과 겹치면 밀어내는 양을 평균 낸다 — 양쪽에서 낀 적이 번갈아 밀려 떨지 않게.
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
        Camera mainCamera;
        readonly Collider2D[] neighbors = new Collider2D[16]; // 반경 안 이웃. 서펜트 사슬이 뭉쳐 지나도 넘치지 않을 만큼
        ContactFilter2D wallFilter;
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];

        protected EnemyData Data => data;
        protected Rigidbody2D Body { get; private set; }
        protected SpriteRenderer Sprite { get; private set; }
        // 몸의 콜라이더. 뚫을 수 없는 이웃과의 겹침을 재는 데 쓴다.
        protected Collider2D Shape { get; private set; }
        // 플레이어. 사망해 파괴되면 null이 된다.
        protected Transform Target { get; private set; }

        // 이 스텝에 분리 힘을 받을지. 궤도가 휘면 안 되는 이동(돌진)은 끈다.
        protected virtual bool UseSeparation => true;

        // 이 이웃과 분리 힘을 주고받을지. 쏘는 졸개(EnemyDart)는 발사체처럼 지나가야 하므로 누구도 밀지 않고 밀리지도 않는다 — 자신은 UseSeparation이 꺼져 있고,
        // 다른 적은 여기서 그것을 이웃으로 치지 않는다. 한 몸처럼 움직이는 것(서펜트의 자기 사슬 마디)을 더 빼는 적은 이 규칙 위에 덧붙인다.
        protected virtual bool SeparatesFrom(Collider2D other) => !other.TryGetComponent(out EnemyDart _);

        // 다른 적이 뚫고 들어올 수 없는 몸인지. 겹친 적은 분리 힘과 별개로 겹친 만큼 그 스텝에 바로 밀려난다 — 지나가는 서펜트가 적을 밀어젖힌다.
        protected virtual bool IsSolid => false;

        protected virtual void Awake()
        {
            if (data == null)
                throw new System.Exception($"[PickShoot] {GetType().Name}.data 미설정 ({name}).");
            if (wallLayerMask.value == 0)
                throw new System.Exception($"[PickShoot] {GetType().Name}.wallLayerMask 미설정 ({name}). 넘지 못할 벽 레이어(Wall) 필요.");
            Body = GetComponent<Rigidbody2D>();
            Sprite = GetComponent<SpriteRenderer>();
            Shape = GetComponent<Collider2D>();
            if (Shape == null)
                throw new System.Exception($"[PickShoot] {GetType().Name}: Collider2D 없음 ({name}). 뚫을 수 없는 몸과의 겹침 판정에 필요.");
            wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(wallLayerMask);
            wallFilter.useTriggers = false;
            mainCamera = Camera.main;
            if (mainCamera == null)
                throw new System.Exception($"[PickShoot] {GetType().Name}: MainCamera 없음 ({name}). 화면 가장자리 판정에 필요.");
            if (!mainCamera.orthographic)
                throw new System.Exception($"[PickShoot] {GetType().Name}: MainCamera가 직교가 아님 ({name}). 보이는 영역을 orthographicSize로 계산한다.");
        }

        protected virtual void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) Target = player.transform;
        }

        public virtual void ApplyKnockback(Vector2 velocity)
        {
            knockback = velocity;
        }

        // 지점을 향한 넉백 — 당기는 폭발(Weapons §7)이 쓴다. 넉백은 knockbackDamping으로 선형 감쇠하므로 같은 세기면 가까운 적이 중심을
        // 지나쳐 버린다. 그래서 감쇠가 끝날 때 꼭 center에 닿는 속도(√(2·감쇠·거리))로 당기되 상한을 maxSpeed로 둔다 — 먼 적은 상한만큼만
        // 끌려온다. 넉백을 무시하는 적(ApplyKnockback 재정의)은 당김도 무시한다. 이미 중심이면 아무것도 하지 않는다.
        public void ApplyPullToward(Vector2 center, float maxSpeed)
        {
            // 위치는 transform에서 읽는다 — 꺼진 컴포넌트(서펜트 몸통 마디)에서도 호출되므로 Awake에서 채우는 Body에 기대지 않는다.
            Vector2 toCenter = center - (Vector2)transform.position;
            float distance = toCenter.magnitude;
            if (distance < 0.0001f) return;
            float speed = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * knockbackDamping * distance));
            ApplyKnockback(toCenter / distance * speed);
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
            Vector2 pushOut = Vector2.zero;
            if (separate) velocity += ComputeSeparation(out pushOut);

            Vector2 move = velocity * Time.fixedDeltaTime + pushOut;
            Vector2 attempted = move;
            bool blockedX = ClampToWall(ref move.x, Vector2.right);
            bool blockedY = ClampToWall(ref move.y, Vector2.up);
            Vector2 to = Body.position + move;
            Body.MovePosition(to);
            if (blockedX || blockedY)
                OnWallBlocked(new Vector2(blockedX ? Mathf.Sign(attempted.x) : 0f, blockedY ? Mathf.Sign(attempted.y) : 0f));
            OnMoved(to);
        }

        // 이 물리 스텝의 목표 속도. 방향 전환(Face)도 여기서 한다.
        protected abstract Vector2 Steer();

        // 이 스텝의 이동이 벽에 막혔다. 넉백·분리 힘에 밀려 막힌 것도 포함한다.
        // blockedSide는 막힌 축마다 벽이 있는 쪽의 부호(오른쪽 벽이면 x = 1), 막히지 않은 축은 0.
        protected virtual void OnWallBlocked(Vector2 blockedSide) { }

        // 이 스텝에 옮겨 갈 자리가 정해졌다. MovePosition은 물리 스텝이 지나야 반영되므로 Body.position은 아직 이전 자리다.
        protected virtual void OnMoved(Vector2 to) { }

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

        // 몸의 중심이 화면 밖에 있는 축의 부호 (오른쪽 가장자리를 넘었으면 x = 1). 화면 안이면 0. 탄환이 화면 가장자리에서 튕기는 것과 같은 기준이다.
        protected Vector2 ScreenEdgeSide()
        {
            Vector3 vp = mainCamera.WorldToViewportPoint(Body.position);
            return new Vector2(vp.x < 0f ? -1f : vp.x > 1f ? 1f : 0f, vp.y < 0f ? -1f : vp.y > 1f ? 1f : 0f);
        }

        // 카메라에 보이는 월드 영역을 가장자리에서 inset만큼 안쪽으로 줄인 사각형. 방랑 이동이 경로가 향할 지점을 뽑는 범위.
        protected Rect VisibleWorldRect(float inset)
        {
            float halfH = mainCamera.orthographicSize;
            float halfW = halfH * mainCamera.aspect;
            if (inset >= Mathf.Min(halfW, halfH))
                throw new System.Exception($"[PickShoot] {GetType().Name}: 화면 여백({inset})이 보이는 영역의 반폭({halfW:0.##}×{halfH:0.##})을 넘음 ({name}). 뽑을 자리가 없다.");
            Vector2 c = mainCamera.transform.position;
            return new Rect(c.x - halfW + inset, c.y - halfH + inset, 2f * (halfW - inset), 2f * (halfH - inset));
        }

        // 진행 방향이 어느 축에서든 side(축별 부호, 막힌 벽이나 넘은 화면 가장자리)를 향하는지. 밀려서 닿은 것과 스스로 향한 것을 가른다.
        protected static bool HeadsInto(Vector2 heading, Vector2 side) => heading.x * side.x > 0f || heading.y * side.y > 0f;

        protected void Face(float dx)
        {
            if (Mathf.Abs(dx) < facingDeadZone) return;
            Sprite.flipX = dx < 0f;
        }

        // 이웃(적 레이어)과의 분리. 거리에 따라 줄어드는 분리 힘(속도)을 돌려주고, 뚫을 수 없는 이웃(IsSolid)과 겹친 만큼은 pushOut(이 스텝의 이동량)에 담는다.
        Vector2 ComputeSeparation(out Vector2 pushOut)
        {
            pushOut = Vector2.zero;
            if (separationStrength <= 0f || separationRadius <= 0f) return Vector2.zero;

            int count = Physics2D.OverlapCircleNonAlloc(Body.position, separationRadius, neighbors, enemyLayerMask);
            Vector2 push = Vector2.zero;
            int solids = 0;
            for (int i = 0; i < count; i++)
            {
                var c = neighbors[i];
                if (c == null || c.attachedRigidbody == Body || !SeparatesFrom(c)) continue;
                Vector2 diff = Body.position - (Vector2)c.transform.position;
                float d = diff.magnitude;
                if (d < 0.0001f) continue;
                Vector2 away = diff / d;
                push += away * (1f - Mathf.Clamp01(d / separationRadius));
                if (c.TryGetComponent(out EnemyMover other) && other.IsSolid)
                {
                    // 콜라이더끼리 겹친 깊이(거리가 음수)만큼 상대에게서 먼 쪽으로 물러난다. 둘 다 뚫을 수 없는 몸이면 반씩.
                    float depth = -Shape.Distance(c).distance;
                    if (depth > 0f)
                    {
                        pushOut += away * depth * (IsSolid ? 0.5f : 1f);
                        solids++;
                    }
                }
            }
            // 여럿과 겹치면 평균만큼만 물러난다. 양쪽에서 각각 겹친 만큼 온전히 밀면 매 스텝 깊이가 뒤바뀌며 왕복한다(떨림).
            // 평균이면 낀 적은 겹침이 같아지는 자리에 머물고, 몸이 움직이는 만큼만 끌려간다.
            if (solids > 1) pushOut /= solids;
            return push * separationStrength;
        }
    }
}
