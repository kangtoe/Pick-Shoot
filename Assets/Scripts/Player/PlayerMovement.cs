using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // WASD 가속·마찰 이동 + 대시의 몸놀림. 걷기 애니메이션은 이동 입력이 있는 동안만 재생하고,
    // 입력을 놓은 뒤 관성으로 미끄러지는 동안은 정지 포즈를 유지한다.
    // 대시(GDD §3.5)는 시작 순간 속도를 한 번 실어 주고 끝날 때까지 가속·마찰을 멈추는 방식이다 — 그동안 속도는 물리에 맡기므로
    // 벽에 닿으면 평소처럼 튕겨 나온다. 끝나면 속도를 이동 속도 이하로 잘라 평소 이동으로 매끄럽게 이어진다.
    // 대시 중에는 지나온 궤적에 일정 간격으로 잔상(DashAfterimage)을 남기고, 회피 대상 레이어(적·추후 적 탄환)와는 충돌하지 않아
    // 몸이 그대로 통과한다. 이 회피는 대시가 끝난 뒤에도 짧은 유예(dashEvadeGrace) 동안 이어진다 — 적과 겹친 채 끝나도 빠져나올
    // 틈을 준다. 회피 중 피해를 받지 않는 것은 PlayerHealth가 IsEvading을 보고 처리한다.
    // 이동속도는 PlayerStats의 배율(유물, Relics §2)을 받는다 — 목표 속도와 대시 뒤 클램프에 쓴다. 가속·마찰은 그대로라 빨라진 만큼 최고 속도에 닿기까지
    // 조금 더 걸린다. 대시 거리·시간은 바뀌지 않는다.
    // 대시는 스페이스 능력의 하나다 (Characters §2) — 입력·쿨다운·효과음은 PlayerAbility가 맡아 RequestDash로 부르고, 거리·유예는 캐릭터 자산(CharacterData)
    // 값으로 함께 넘어온다. 여기는 몸을 옮기고 회피를 켜고 끄는 일만 한다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(SpriteWalkCycle))]
    [RequireComponent(typeof(PlayerAiming))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float acceleration = 40f;
        [SerializeField] float friction = 15f;
        [SerializeField, Range(0f, 1f)] float wallBounceRetention = 0.55f;

        [Header("Dash")]
        // 대시 한 번에 걸리는 시간. 속도 = 거리(캐릭터 자산) ÷ 시간. 시간은 물리 스텝 단위로 반올림된다.
        [SerializeField, Min(0.01f)] float dashDuration = 0.1f;
        // 회피 중 몸이 통과하는 레이어 (적, 추후 적 탄환). 벽은 넣지 않는다 — 대시로 아레나를 벗어날 수 없어야 한다.
        [SerializeField] LayerMask dashPassThroughLayers;
        // 잔상 사이의 거리 (월드 유닛). 프레임 단위가 아니라 궤적 위에 이 간격으로 놓으므로 프레임률과 무관하게 고르다.
        [SerializeField, Min(0.05f)] float afterimageSpacing = 0.5f;
        // 잔상 한 장이 사라지기까지 (초).
        [SerializeField, Min(0.01f)] float afterimageDuration = 0.25f;
        // 잔상 색. 스프라이트 색에 곱해지며 알파는 처음 불투명도 — 여기서 0으로 옅어진다.
        [SerializeField] Color afterimageColor = new Color(0.6f, 0.85f, 1f, 0.6f);

        Rigidbody2D rb;
        SpriteWalkCycle walkCycle;
        PlayerAiming aiming;
        SpriteRenderer sr;
        PlayerStats stats;
        Vector2 input;
        // 다음 물리 스텝에 시작할 대시 (RequestDash). 거리·유예는 요청과 함께 온 캐릭터 자산 값.
        bool dashRequested;
        float requestedDashDistance;
        float requestedEvadeGrace;
        // 대시가 끝나기까지 남은 시간. 0보다 크면 대시 중.
        float dashTimeLeft;
        // 지금 대시가 끝난 뒤 이어질 회피 유예 (초).
        float dashEvadeGrace;
        bool evading;
        // 대시가 끝난 뒤 회피가 풀리는 시각. 대시 중에는 쓰지 않는다.
        float evadeEndTime;
        // 회피 중이 아닐 때의 Rigidbody2D.excludeLayers. 회피가 끝나면 이 값으로 되돌린다.
        LayerMask baseExcludeLayers;
        // 마지막 잔상을 놓은 자리. 여기서 afterimageSpacing만큼 멀어질 때마다 다음 잔상을 놓는다.
        Vector2 lastAfterimagePosition;

        // 대시 중이거나 다음 물리 스텝의 시작을 기다리는 중 — 이 동안은 다음 대시를 요청할 수 없다.
        public bool IsDashing => dashRequested || InDashMotion;
        // 대시로 몸이 나아가는 중 (시작 스텝부터 끝 스텝까지). 잔상·걷기 멈춤은 이것만 본다 — 요청만 받은 프레임에 잔상을 놓으면 지난 대시의 끝자리부터 이어 그린다.
        bool InDashMotion => dashTimeLeft > 0f;
        // 회피 중 = 대시 중 + 끝난 뒤 유예. 이동안 적을 통과하고 피해를 받지 않는다.
        public bool IsEvading => evading;
        // 유물 배율을 받은 이동속도 (Relics §2).
        float EffectiveMoveSpeed => moveSpeed * stats.MoveSpeedMultiplier;
        // 배율을 곱하기 전의 자산 값. 보상 화면의 상세 패널이 유물의 효과를 "지금 → 다음"의 실제 값으로 보여줄 때 쓴다.
        public float BaseMoveSpeed => moveSpeed;

        // 대시 능력의 발동 (PlayerAbility). 다음 물리 스텝에 시작한다. 방향은 그 스텝의 이동 입력, 없으면 조준 방향.
        public void RequestDash(float distance, float evadeGrace)
        {
            if (IsDashing)
                throw new System.Exception("[PickShoot] PlayerMovement.RequestDash: 이미 대시 중. PlayerAbility가 IsDashing을 보고 입력을 버려야 함.");
            if (distance <= 0f || evadeGrace < 0f)
                throw new System.Exception($"[PickShoot] PlayerMovement.RequestDash: 거리({distance})는 양수, 유예({evadeGrace})는 0 이상이어야 함.");
            dashRequested = true;
            requestedDashDistance = distance;
            requestedEvadeGrace = evadeGrace;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            walkCycle = GetComponent<SpriteWalkCycle>();
            aiming = GetComponent<PlayerAiming>();
            sr = GetComponent<SpriteRenderer>();
            stats = GetComponent<PlayerStats>();
            if (stats == null)
                throw new System.Exception("[PickShoot] PlayerMovement: 플레이어에 PlayerStats 없음 (이동속도 배율 출처).");
            if (dashPassThroughLayers.value == 0)
                throw new System.Exception("[PickShoot] PlayerMovement.dashPassThroughLayers 미설정. 대시 중 통과할 레이어(Enemy 등) 필요.");
            baseExcludeLayers = rb.excludeLayers;
        }

        void Update()
        {
            input = ReadInput();
            // 대시 중에는 걷기 프레임이 거리에 비례해 빠르게 깜빡이므로 정지 포즈로 둔다.
            walkCycle.Propelled = input.sqrMagnitude > 0.0001f && !InDashMotion;
            if (InDashMotion) LeaveAfterimages();
        }

        // 마지막 잔상 자리에서 지금 자리까지의 궤적을 간격대로 채운다. 화면에 보이는 위치(보간된 transform) 기준이라
        // 잔상이 캐릭터가 실제로 그려진 자리에 남는다.
        void LeaveAfterimages()
        {
            Vector2 pos = transform.position;
            while (Vector2.Distance(lastAfterimagePosition, pos) >= afterimageSpacing)
            {
                lastAfterimagePosition = Vector2.MoveTowards(lastAfterimagePosition, pos, afterimageSpacing);
                DashAfterimage.Spawn(sr, sr.sharedMaterial, lastAfterimagePosition, afterimageColor, afterimageDuration);
            }
        }

        static Vector2 ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        void FixedUpdate()
        {
            if (dashRequested)
            {
                dashRequested = false;
                StartDash();
            }

            if (InDashMotion)
            {
                dashTimeLeft -= Time.fixedDeltaTime;
                // 부동소수 오차로 스텝 하나가 더 돌거나 덜 돌지 않도록 반 스텝 기준으로 끝을 판정한다.
                if (dashTimeLeft > Time.fixedDeltaTime * 0.5f) return;
                dashTimeLeft = 0f;
                evadeEndTime = Time.time + dashEvadeGrace;
                rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, EffectiveMoveSpeed);
            }

            // 유예까지 지나면 충돌을 되살린다. 이 순간에도 적과 겹쳐 있으면 접촉이 새로 시작돼 평소대로 피해를 입는다 (GDD §3.5).
            if (evading && Time.time >= evadeEndTime)
            {
                evading = false;
                rb.excludeLayers = baseExcludeLayers;
            }

            var target = input * EffectiveMoveSpeed;
            float rate = input.sqrMagnitude > 0.0001f ? acceleration : friction;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * Time.fixedDeltaTime);
        }

        // 방향은 이동 입력, 입력이 없으면 조준 방향.
        void StartDash()
        {
            Vector2 dir = input.sqrMagnitude > 0.0001f ? input.normalized : aiming.AimDirection;
            rb.linearVelocity = dir * (requestedDashDistance / dashDuration);
            // 시작한 스텝도 대시 한 스텝으로 센다 (FixedUpdate가 곧바로 한 스텝을 뺀다).
            dashTimeLeft = dashDuration + Time.fixedDeltaTime;
            dashEvadeGrace = requestedEvadeGrace;
            evading = true;
            rb.excludeLayers = baseExcludeLayers | dashPassThroughLayers;

            // 첫 잔상은 떠난 자리에.
            lastAfterimagePosition = transform.position;
            DashAfterimage.Spawn(sr, sr.sharedMaterial, lastAfterimagePosition, afterimageColor, afterimageDuration);
        }

        void OnCollisionEnter2D(Collision2D _)
        {
            rb.linearVelocity *= wallBounceRetention;
        }
    }
}
