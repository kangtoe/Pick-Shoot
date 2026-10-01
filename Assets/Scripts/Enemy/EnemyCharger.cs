using UnityEngine;

namespace PickShoot
{
    // 돌진형 적 (Enemies §4). 접근 → 준비 → 돌진 → 회복을 돈다.
    //   접근: 플레이어를 향해 느리게 추적. 트리거 거리에 들어오면 준비로.
    //   준비: 정지. 돌진 방향은 이 순간의 플레이어 방향으로 고정하고 그쪽을 본다. 머리 위에 느낌표(EnemyAttackWarning)를 띄우고,
    //         걷기 프레임 대신 액션 포즈와 정지 포즈를 번갈아 깜빡이며 끝이 가까우면 빨라진다 (SpawnWarningMarker와 같은 문법).
    //         색은 쓰지 않는다 — 히트 플래시가 실루엣을 통째로 덮어 맞는 동안 가려진다. 느낌표는 돌진이 시작되는 순간 내린다.
    //   돌진: 고정 방향으로 고속 직진. 액션 포즈를 고정하고 궤적에 잔상(DashAfterimage)을 남긴다. 벽·최대 거리·플레이어 접촉으로 끝난다.
    //         벽은 공통 골격(EnemyMover)이 막고 OnWallBlocked로 알려 준다.
    //         잔상의 머티리얼은 몸에서 베끼지 않고 EnemyHealth.BodyMaterial을 넘긴다 — 점멸 중에 맞으면 몸의 머티리얼이 잠시 흰 실루엣이라 잔상이 하얗게 굳는다.
    //         플레이어에 닿으면 끝내야 한다 — 끝내지 않으면 동적인 플레이어를 돌진 속도로 벽까지 밀고 간다.
    //         분리 힘은 받지 않는다 (무리에 밀려 궤도가 휘지 않게). 넉백은 모든 단계에서 그대로 받는다.
    //   회복: 정지 포즈로 잠시 멈춘 뒤 접근으로.
    // 거리·시간·속도는 ChargerData 자산이 정한다. 준비·돌진·회복 동안은 SpriteWalkCycle을 끄고 포즈를 직접 정한다.
    [RequireComponent(typeof(SpriteWalkCycle))]
    [RequireComponent(typeof(EnemyAttackWarning))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyCharger : EnemyMover
    {
        enum State { Approach, Windup, Charge, Recover }

        [SerializeField] Sprite chargePose;      // 준비 깜빡임과 돌진 중 고정하는 포즈 (시트의 액션 프레임)
        [SerializeField, Min(0.01f)] float windupBlinkFrequency = 2f;
        [SerializeField, Min(0f)] float windupFinalBlinkDuration = 0.3f;
        [SerializeField, Min(0.01f)] float windupFinalBlinkFrequency = 8f;
        [SerializeField] AudioClip chargeClip;
        [SerializeField, Min(0.05f)] float afterimageSpacing = 0.5f;
        [SerializeField, Min(0.01f)] float afterimageDuration = 0.2f;
        [SerializeField] Color afterimageColor = new Color(1f, 0.75f, 0.6f, 0.6f);

        ChargerData charger;
        SpriteWalkCycle walkCycle;
        EnemyAttackWarning attackWarning;
        EnemyHealth health;
        State state = State.Approach;
        float stateStartTime;
        Vector2 chargeDirection;
        float chargeTraveled;
        // 돌진 중 플레이어에 닿았다. 충돌 콜백이 세우고 다음 스텝의 Steer가 소비한다.
        bool hitPlayer;
        Vector2 lastAfterimagePosition;

        protected override bool UseSeparation => state != State.Charge;

        protected override void Awake()
        {
            base.Awake();
            charger = Data as ChargerData;
            if (charger == null)
                throw new System.Exception($"[PickShoot] EnemyCharger.data가 ChargerData 자산이 아님 ({name}: {Data.name}).");
            if (charger.ChargeSpeed <= 0f)
                throw new System.Exception($"[PickShoot] ChargerData.chargeSpeed가 0 이하 ({Data.name}). 돌진이 끝나지 않는다.");
            if (chargePose == null)
                throw new System.Exception($"[PickShoot] EnemyCharger.chargePose 미설정 ({name}).");
            walkCycle = GetComponent<SpriteWalkCycle>();
            attackWarning = GetComponent<EnemyAttackWarning>();
            health = GetComponent<EnemyHealth>();
        }

        protected override Vector2 Steer()
        {
            switch (state)
            {
                case State.Approach: return SteerApproach();
                case State.Windup: return SteerWindup();
                case State.Charge: return SteerCharge();
                case State.Recover: return SteerRecover();
                default: throw new System.Exception($"[PickShoot] EnemyCharger: 알 수 없는 상태 {state} ({name}).");
            }
        }

        Vector2 SteerApproach()
        {
            if (Target == null) return Vector2.zero;
            Vector2 delta = (Vector2)Target.position - Body.position;
            Face(delta.x);
            if (delta.sqrMagnitude <= charger.ChargeTriggerDistance * charger.ChargeTriggerDistance)
            {
                BeginWindup(delta);
                return Vector2.zero;
            }
            return delta.normalized * Data.MoveSpeed;
        }

        Vector2 SteerWindup()
        {
            if (Time.time - stateStartTime < charger.WindupDuration) return Vector2.zero;
            BeginCharge();
            return SteerCharge();
        }

        // 이번 스텝에 나아갈 거리를 정한다. 최대 거리의 남은 몫까지만 나아가고, 다 달렸으면 이 스텝을 끝으로 회복에 든다.
        // 플레이어에 닿은 것은 이전 스텝의 충돌 콜백이 알려 주므로 더 나아가지 않고 그 자리에서 끝낸다.
        Vector2 SteerCharge()
        {
            if (hitPlayer)
            {
                BeginRecover();
                return Vector2.zero;
            }

            float dt = Time.fixedDeltaTime;
            float step = charger.ChargeSpeed * dt;
            float remaining = charger.ChargeMaxDistance - chargeTraveled;
            bool lastStep = remaining <= step;
            float travel = lastStep ? remaining : step;

            chargeTraveled += travel;
            if (lastStep) BeginRecover();
            return chargeDirection * (travel / dt);
        }

        // 돌진 중 벽에 막히면 그 자리에서 끝낸다. 다른 단계에서는 공통 골격이 멈춰 세우는 것으로 충분하다.
        protected override void OnWallBlocked(Vector2 blockedSide)
        {
            if (state == State.Charge) BeginRecover();
        }

        Vector2 SteerRecover()
        {
            if (Time.time - stateStartTime >= charger.RecoveryDuration) BeginApproach();
            return Vector2.zero;
        }

        void BeginWindup(Vector2 toPlayer)
        {
            state = State.Windup;
            stateStartTime = Time.time;
            // 플레이어와 겹쳐 방향이 없으면 보고 있던 쪽으로.
            chargeDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : (Sprite.flipX ? Vector2.left : Vector2.right);
            Face(chargeDirection.x);
            walkCycle.enabled = false;
            Sprite.sprite = chargePose;
            attackWarning.Show();
        }

        void BeginCharge()
        {
            state = State.Charge;
            stateStartTime = Time.time;
            chargeTraveled = 0f;
            hitPlayer = false;
            Sprite.sprite = chargePose;
            attackWarning.Hide();
            // 첫 잔상은 떠나는 자리에.
            lastAfterimagePosition = transform.position;
            DashAfterimage.Spawn(Sprite, health.BodyMaterial, lastAfterimagePosition, afterimageColor, afterimageDuration);
            if (chargeClip != null) SoundPlayer.Play(chargeClip);
        }

        void BeginRecover()
        {
            state = State.Recover;
            stateStartTime = Time.time;
            Sprite.sprite = walkCycle.IdleFrame;
        }

        void BeginApproach()
        {
            state = State.Approach;
            stateStartTime = Time.time;
            walkCycle.enabled = true; // 다시 켜지면 기준 위치를 새로 잡아 그동안 옮긴 거리를 걷기로 세지 않는다
        }

        void Update()
        {
            if (state == State.Windup) Sprite.sprite = WindupPoseVisible() ? chargePose : walkCycle.IdleFrame;
            else if (state == State.Charge) LeaveAfterimages();
        }

        // 포즈·정지 같은 길이로 교대. 마지막 구간은 그 구간의 시작부터 세어 포즈 상태로 들어간다 (SpawnWarningMarker와 같은 계산).
        bool WindupPoseVisible()
        {
            float elapsed = Time.time - stateStartTime;
            float finalStart = charger.WindupDuration - windupFinalBlinkDuration;
            return elapsed < finalStart
                ? (Mathf.FloorToInt(elapsed * windupBlinkFrequency * 2f) & 1) == 0
                : (Mathf.FloorToInt((elapsed - finalStart) * windupFinalBlinkFrequency * 2f) & 1) == 0;
        }

        // 마지막 잔상 자리에서 지금 자리까지의 궤적을 간격대로 채운다 (PlayerMovement의 대시 잔상과 같은 방식).
        void LeaveAfterimages()
        {
            Vector2 pos = transform.position;
            while (Vector2.Distance(lastAfterimagePosition, pos) >= afterimageSpacing)
            {
                lastAfterimagePosition = Vector2.MoveTowards(lastAfterimagePosition, pos, afterimageSpacing);
                DashAfterimage.Spawn(Sprite, health.BodyMaterial, lastAfterimagePosition, afterimageColor, afterimageDuration);
            }
        }

        // 회피 중인 플레이어는 충돌 자체가 꺼져 있어 여기 오지 않고 그대로 통과한다.
        void OnCollisionEnter2D(Collision2D col) => NoteContact(col);
        void OnCollisionStay2D(Collision2D col) => NoteContact(col);

        void NoteContact(Collision2D col)
        {
            if (state != State.Charge) return;
            if (col.collider.GetComponent<PlayerHealth>() == null) return;
            hitPlayer = true;
        }
    }
}
