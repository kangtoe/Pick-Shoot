using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // WASD 가속·마찰 이동 + 스페이스 대시. 걷기 애니메이션은 이동 입력이 있는 동안만 재생하고,
    // 입력을 놓은 뒤 관성으로 미끄러지는 동안은 정지 포즈를 유지한다.
    // 대시(GDD §3.5)는 시작 순간 속도를 한 번 실어 주고 끝날 때까지 가속·마찰을 멈추는 방식이다 — 그동안 속도는 물리에 맡기므로
    // 벽에 닿으면 평소처럼 튕겨 나온다. 끝나면 속도를 이동 속도 이하로 잘라 평소 이동으로 매끄럽게 이어진다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteWalkCycle))]
    [RequireComponent(typeof(PlayerAiming))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float acceleration = 40f;
        [SerializeField] float friction = 15f;
        [SerializeField, Range(0f, 1f)] float wallBounceRetention = 0.55f;

        [Header("Dash")]
        // 대시 한 번에 나아가는 거리(월드 유닛)와 걸리는 시간. 속도 = 거리 ÷ 시간. 시간은 물리 스텝 단위로 반올림된다.
        [SerializeField, Min(0f)] float dashDistance = 3f;
        [SerializeField, Min(0.01f)] float dashDuration = 0.1f;
        // 대시를 시작한 순간부터 다음 대시가 가능해질 때까지 (초).
        [SerializeField, Min(0f)] float dashCooldown = 1.5f;

        Rigidbody2D rb;
        SpriteWalkCycle walkCycle;
        PlayerAiming aiming;
        Vector2 input;
        bool dashRequested;
        // 대시가 끝나기까지 남은 시간. 0보다 크면 대시 중.
        float dashTimeLeft;
        float dashStartTime;
        float dashReadyTime;

        public bool IsDashing => dashTimeLeft > 0f;
        public bool DashReady => Time.time >= dashReadyTime;
        // 대시 쿨다운 진행도. 0 = 방금 대시함, 1 = 사용 가능. GameHud의 머리 위 원형 표시가 읽는다.
        public float DashCooldownProgress => DashReady ? 1f : Mathf.InverseLerp(dashStartTime, dashReadyTime, Time.time);

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            walkCycle = GetComponent<SpriteWalkCycle>();
            aiming = GetComponent<PlayerAiming>();
        }

        void Update()
        {
            input = ReadInput();
            // 게임이 멈춘 동안(일시정지·카드 선택, timeScale 0)의 입력은 받지 않는다 — 재개하자마자 대시가 나가지 않게.
            // 쿨다운 중의 입력은 버린다 (선입력 없음).
            if (Time.timeScale > 0f && DashPressed() && DashReady && !IsDashing)
                dashRequested = true;
            // 대시 중에는 걷기 프레임이 거리에 비례해 빠르게 깜빡이므로 정지 포즈로 둔다.
            walkCycle.Propelled = input.sqrMagnitude > 0.0001f && !IsDashing;
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

        static bool DashPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        void FixedUpdate()
        {
            if (dashRequested)
            {
                dashRequested = false;
                StartDash();
            }

            if (IsDashing)
            {
                dashTimeLeft -= Time.fixedDeltaTime;
                // 부동소수 오차로 스텝 하나가 더 돌거나 덜 돌지 않도록 반 스텝 기준으로 끝을 판정한다.
                if (dashTimeLeft > Time.fixedDeltaTime * 0.5f) return;
                dashTimeLeft = 0f;
                rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, moveSpeed);
            }

            var target = input * moveSpeed;
            float rate = input.sqrMagnitude > 0.0001f ? acceleration : friction;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * Time.fixedDeltaTime);
        }

        // 방향은 이동 입력, 입력이 없으면 조준 방향.
        void StartDash()
        {
            Vector2 dir = input.sqrMagnitude > 0.0001f ? input.normalized : aiming.AimDirection;
            rb.linearVelocity = dir * (dashDistance / dashDuration);
            // 시작한 스텝도 대시 한 스텝으로 센다 (FixedUpdate가 곧바로 한 스텝을 뺀다).
            dashTimeLeft = dashDuration + Time.fixedDeltaTime;
            dashStartTime = Time.time;
            dashReadyTime = dashStartTime + dashCooldown;
        }

        void OnCollisionEnter2D(Collision2D _)
        {
            rb.linearVelocity *= wallBounceRetention;
        }
    }
}
