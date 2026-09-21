using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] float decayLinearDamping = 5f;
        [SerializeField] float wallBounceMinSpeed = 7f;
        [SerializeField, Range(0f, 89f)] float maxBounceAngleFromNormal = 75f;
        [SerializeField] float bounceFlashDuration = 0.09f;
        [SerializeField] Color bounceFlashColor = Color.white;
        [SerializeField] float bounceScalePulse = 1.5f;
        [SerializeField] AudioClip bounceClip;
        // 거의 모든 탄환이 한 번은 벽·가장자리에 닿으므로 다른 효과음보다 낮춰 깔아 둔다.
        [SerializeField, Range(0f, 1f)] float bounceVolume = 0.5f;
        [SerializeField] float separationRadius = 0.6f;
        [SerializeField] float separationStrength = 3f;
        [SerializeField] LayerMask bulletLayerMask;
        [SerializeField] GameObject hitEffectPrefab;

        const string ActiveLayerName = "Bullet";
        const string InactiveLayerName = "BulletInactive";

        Camera mainCam;
        Rigidbody2D rb;
        SpriteRenderer sr;
        Collider2D bodyCollider;
        Vector2 lastVelocity;
        Vector3 baseScale;
        Color baseColor;
        bool active;
        float flashUntil;
        bool flashing;
        int activeLayerIdx;
        int inactiveLayerIdx;
        bool wasActive;
        Weapon ownerWeapon;
        int damage;
        float knockbackForce;
        int remainingBounces;
        int remainingPenetration;
        readonly Collider2D[] neighbors = new Collider2D[8];

        // active=true: 튕김 페이즈 (속도·충돌판정 유지). active=false: 정착 (감속·픽업·Inactive 레이어).
        public bool IsSettled => !active;
        public Weapon OwnerWeapon => ownerWeapon;

        // 플레이어 자석에 한 번 걸리면 회수될 때까지 풀리지 않는다. 걸기·끌림 세기(0~1) 갱신은 PlayerPickup이 한다.
        public bool IsMagnetized { get; private set; }
        public float MagnetPull { get; set; }

        // 끌려오는 탄환은 아무것에도 막히지 않는다. 콜라이더를 꺼 벽에 걸리지 않게 하고, 다른 탄환의
        // 분리 탐색(OverlapCircle)에도 잡히지 않게 한다. 자신이 받는 분리 힘은 FixedUpdate에서 건너뛴다.
        public void Magnetize()
        {
            if (active)
                throw new System.Exception("[PickShoot] Bullet.Magnetize: 튕김 페이즈의 탄환은 자석에 걸 수 없습니다. 정착(ForceSettle) 후 호출해야 합니다.");
            IsMagnetized = true;
            bodyCollider.enabled = false;
        }

        // 웨이브 클리어 sweep 등에서 튕김 페이즈를 강제 종료해 회수 대상으로 만든다.
        // FixedUpdate가 다음 프레임에 damping·layer를 정착 상태로 반영한다.
        public void ForceSettle() => active = false;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider == null)
                throw new System.Exception("[PickShoot] Bullet에 Collider2D가 필요합니다.");
            if (sr != null) baseColor = sr.color;
            baseScale = transform.localScale;

            mainCam = Camera.main;
            if (mainCam == null)
                throw new System.Exception("[PickShoot] MainCamera가 필요합니다 (탄환 카메라 가장자리 튕김용).");

            activeLayerIdx = LayerMask.NameToLayer(ActiveLayerName);
            inactiveLayerIdx = LayerMask.NameToLayer(InactiveLayerName);
            if (activeLayerIdx < 0 || inactiveLayerIdx < 0)
                throw new System.Exception($"[PickShoot] 레이어 미설정: '{ActiveLayerName}' / '{InactiveLayerName}'. Project Settings > Tags and Layers에서 추가 필요.");
        }

        public void Launch(Vector2 velocity, Weapon owner, int damage, float knockbackForce, int maxBounces, int penetration)
        {
            if (owner == null)
                throw new System.Exception("[PickShoot] Bullet.Launch: owner Weapon이 null. 탄환은 반드시 무기에 귀속되어야 회수 시 탄창 충전이 가능합니다.");
            if (maxBounces < 0)
                throw new System.Exception($"[PickShoot] Bullet.Launch: maxBounces는 0 이상이어야 합니다. 전달값={maxBounces}");
            if (penetration < 0)
                throw new System.Exception($"[PickShoot] Bullet.Launch: penetration은 0 이상이어야 합니다. 전달값={penetration}");
            ownerWeapon = owner;
            this.damage = damage;
            this.knockbackForce = knockbackForce;
            remainingBounces = maxBounces;
            remainingPenetration = penetration;

            // 발사한 무기의 색을 탄환 색으로 반영 (baseColor는 튕김 flash 복원용).
            if (sr != null)
            {
                baseColor = owner.Data.VisualColor;
                sr.color = baseColor;
            }

            // 발사 직후는 항상 튕김 페이즈. 첫 충돌 시점에 남은 튕김 카운트를 보고
            // 튕기거나(>0) 그 자리에서 정착(=0)한다.
            active = true;
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
            if (active) HandleCameraEdgeBounce();

            if (!active)
                rb.linearDamping = decayLinearDamping;

            if (!IsMagnetized)
            {
                Vector2 separation = ComputeSeparation();
                if (separation.sqrMagnitude > 0.0001f)
                    rb.linearVelocity += separation * Time.fixedDeltaTime;
            }

            lastVelocity = rb.linearVelocity;

            if (active != wasActive)
            {
                gameObject.layer = active ? activeLayerIdx : inactiveLayerIdx;
                wasActive = active;
            }
        }

        void HandleCameraEdgeBounce()
        {
            Vector3 vp = mainCam.WorldToViewportPoint(rb.position);
            Vector2 normal = Vector2.zero;
            if (vp.x < 0f) normal += Vector2.right;
            else if (vp.x > 1f) normal += Vector2.left;
            if (vp.y < 0f) normal += Vector2.up;
            else if (vp.y > 1f) normal += Vector2.down;

            if (normal.sqrMagnitude < 0.0001f) return;
            normal.Normalize();

            Vector2 vel = rb.linearVelocity;
            if (Vector2.Dot(vel, normal) >= 0f) return; // 이미 안쪽으로 향함 → 튕기지 않음.

            // 위치는 어느 분기든 viewport 안으로 복귀시켜 재검출 방지.
            Vector3 clampedVp = new Vector3(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y), vp.z);
            rb.position = mainCam.ViewportToWorldPoint(clampedVp);
            PlayBounceSound();

            if (remainingBounces > 0)
            {
                Vector2 reflected = Vector2.Reflect(vel, normal);
                Vector2 clampedDir = ClampDirectionToNormalCone(reflected.normalized, normal);
                float outSpeed = Mathf.Max(reflected.magnitude, wallBounceMinSpeed);
                rb.linearVelocity = clampedDir * outSpeed;
                TriggerBounceEffect();
                DecrementBounce();
            }
            else
            {
                // 튕김 카운트 소진 → 벽과 같은 모습으로 정착. 벽에서는 물리엔진(반발 1·마찰 0)이
                // 거울 반사를 해 주지만 화면 가장자리엔 콜라이더가 없으므로 직접 반사한 뒤 감속에 맡긴다.
                rb.linearVelocity = Vector2.Reflect(vel, normal);
                active = false;
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
            if (!active) return;

            var enemy = col.collider.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                HandleEnemyHit(col, enemy);
                return;
            }

            PlayBounceSound();
            if (remainingBounces > 0)
            {
                HandleWallBounce(col);
                DecrementBounce();
            }
            else
            {
                // 튕김 카운트 소진 → 접촉 지점에서 정착. 물리엔진 반사에 관성 맡기고 감속.
                active = false;
            }
        }

        void HandleEnemyHit(Collision2D col, EnemyHealth enemy)
        {
            enemy.TakeDamage(damage);

            var movement = col.collider.GetComponent<EnemyMovement>();
            if (movement != null && lastVelocity.sqrMagnitude > 0.01f)
                movement.ApplyKnockback(lastVelocity.normalized * knockbackForce);

            if (hitEffectPrefab != null)
            {
                Vector3 hitPos = col.contactCount > 0 ? (Vector3)col.GetContact(0).point : transform.position;
                Instantiate(hitEffectPrefab, hitPos, Quaternion.identity);
            }

            if (remainingPenetration > 0)
            {
                // 관통 카운트 남음 → 통과. 속도·방향 유지, 튕김 카운트 미소모.
                remainingPenetration--;
                rb.linearVelocity = lastVelocity;
                return;
            }

            // 관통 소진 → 벽과 동일 규칙.
            if (remainingBounces > 0)
            {
                HandleWallBounce(col);
                DecrementBounce();
            }
            else
            {
                active = false;
            }
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

        void DecrementBounce()
        {
            remainingBounces--;
            if (remainingBounces <= 0)
                active = false;
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

        // 튕김 페이즈의 탄환이 벽·화면 가장자리에 닿을 때마다 울린다. 튕김 카운트가 남았든(반사 후 계속 비행)
        // 소진됐든(반사 후 정착) 화면에서는 똑같이 튕겨 보이므로 구분하지 않는다. 적에게 튕길 때는 명중음이 대신한다.
        void PlayBounceSound()
        {
            if (bounceClip != null) SoundPlayer.Play(bounceClip, bounceVolume);
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
