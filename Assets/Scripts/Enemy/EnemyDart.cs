using UnityEngine;

namespace PickShoot
{
    // 소환사가 쏘는 졸개 (Enemies §6). 발사체에 가까운 적이다 — 나온 순간의 플레이어 방향으로 직진만 하고, 벽이나 화면 가장자리에 닿으면 죽는다.
    //   방향: Start에서 플레이어를 향해 잡고 그 뒤로는 쫓거나 되돌지 않는다. 플레이어가 없거나 겹친 자리면 예외 — 낳는 쪽(소환사·WaveManager)은 플레이어가 살아 있고
    //         마커 가까이 없을 때만 낳기로 되어 있다. 디버그 도구로 바로 스폰해도 플레이어 주변 스폰 거리에 놓이므로 그대로 쏜다. 바라보는 방향은 가는 방향이다.
    //   벽: 공통 골격이 막힌 쪽을 알려 주는 스텝에 죽는다 — 넉백에 밀려 닿은 것도 같다.
    //   화면 가장자리: 몸의 중심이 카메라 뷰포트 밖으로 나갔고 진행 방향이 바깥쪽이면 죽는다 — 탄환이 화면 가장자리에서 튕기는 것과 같은 기준.
    //       밖에 있어도 안쪽을 향하면 그대로 간다 — 예고 중에 카메라가 움직여 스폰 자리가 화면 밖이 된 것.
    //   죽음은 EnemyHealth.Kill — 사망 이펙트는 나되 소리는 없다(스스로 꺼지는 것이라). 탄환에 맞아 죽는 것은 평소대로.
    //   분리 힘은 주고받지 않는다(EnemyMover의 기본 규칙) — 뒤따르는 소환사나 다른 적을 밀거나 그들에게 밀려 궤도가 휘지 않게, 발사체처럼 지나간다.
    //   넉백·접촉 데미지는 평소대로. 뚫을 수 없는 몸(IsSolid)은 아니다.
    //   느낌표(EnemyAttackWarning)는 나온 순간부터 죽을 때까지 띄운다 — 몸 자체가 날아오는 공격이라 "지금 비켜야 함"이 처음부터 끝까지다.
    // 같은 그림의 일반 졸개(Enemy_Minion, EnemyChaser)와 다르다 — 그쪽은 소환사가 죽을 때 놓는 보통 추적형이다.
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyAttackWarning))]
    public class EnemyDart : EnemyMover
    {
        EnemyHealth health;
        EnemyAttackWarning attackWarning;
        Vector2 heading;

        protected override bool UseSeparation => false;

        protected override void Awake()
        {
            base.Awake();
            health = GetComponent<EnemyHealth>();
            attackWarning = GetComponent<EnemyAttackWarning>();
        }

        // 플레이어는 공통 골격의 Start에서 잡힌다. 모든 컴포넌트의 Awake가 끝난 뒤라 느낌표도 여기서 켠다.
        protected override void Start()
        {
            base.Start();
            if (Target == null)
                throw new System.Exception($"[PickShoot] EnemyDart: 플레이어 없이 나옴 ({name}). 플레이어가 살아 있을 때만 낳아야 한다.");
            Vector2 delta = (Vector2)Target.position - Body.position;
            if (delta.sqrMagnitude <= 0.0001f)
                throw new System.Exception($"[PickShoot] EnemyDart: 플레이어와 겹친 자리에 나옴 ({name}). 예고 중 플레이어가 가까우면 건너뛰어야 한다.");
            heading = delta.normalized;
            attackWarning.Show();
        }

        protected override Vector2 Steer()
        {
            Vector2 edge = ScreenEdgeSide();
            if (edge != Vector2.zero && HeadsInto(heading, edge))
            {
                health.Kill();
                return Vector2.zero;
            }
            Face(heading.x);
            return heading * MoveSpeed;
        }

        protected override void OnWallBlocked(Vector2 blockedSide) => health.Kill();
    }
}
