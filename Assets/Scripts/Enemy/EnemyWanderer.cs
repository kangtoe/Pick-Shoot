using UnityEngine;

namespace PickShoot
{
    // 방랑형 적 (Enemies §6). 플레이어와 무관하게 무작위 방향으로 등속 직진하고, 두 계기로만 방향을 바꾼다.
    //   시간: 데이터의 범위에서 뽑은 시간이 지나면 아무 방향이나 새로 뽑는다. 방향을 바꿀 때마다 다음 시간을 새로 뽑는다.
    //   벽: 공통 골격이 막힌 쪽을 알려 주면 그 축의 부호를 아레나 안쪽으로 강제한 방향을 뽑는다. 진행 방향이 이미 안쪽인데 넉백·분리 힘에
    //       밀려 막힌 것이면 그대로 둔다 — 벽에 눌린 몇 스텝 동안 매번 방향이 바뀌지 않게.
    // 바라보는 방향은 플레이어가 아니라 진행 방향이다 — 어디로 가는지 읽혀야 피할 수 있다. 상하로만 갈 때는 마지막 좌우를 유지한다(Face의 데드존).
    // 넉백·분리 힘·벽 판정·걷기는 공통 골격 그대로. 수집형도 이 이동을 그대로 쓰고 탄환을 끄는 처리(BulletAttractor)만 얹는다.
    public class EnemyWanderer : EnemyMover
    {
        WandererData wanderer;
        Vector2 heading;
        float nextTurnTime;

        protected override void Awake()
        {
            base.Awake();
            wanderer = Data as WandererData;
            if (wanderer == null)
                throw new System.Exception($"[PickShoot] EnemyWanderer.data가 WandererData 자산이 아님 ({name}: {Data.name}).");
            if (wanderer.TurnIntervalMax < wanderer.TurnIntervalMin)
                throw new System.Exception($"[PickShoot] WandererData의 방향 전환 시간 범위가 비어 있음 ({Data.name}: min {wanderer.TurnIntervalMin} > max {wanderer.TurnIntervalMax}).");
            SetHeading(RandomDirection());
        }

        protected override Vector2 Steer()
        {
            if (Time.time >= nextTurnTime) SetHeading(RandomDirection());
            Face(heading.x);
            return heading * Data.MoveSpeed;
        }

        // 진행 방향이 막힌 쪽을 향할 때만 꺾는다. 새 방향은 막힌 축마다 부호를 벽 반대쪽으로 강제한다 — 구석이면 두 축 모두.
        protected override void OnWallBlocked(Vector2 blockedSide)
        {
            bool intoX = heading.x * blockedSide.x > 0f;
            bool intoY = heading.y * blockedSide.y > 0f;
            if (!intoX && !intoY) return;

            Vector2 d = RandomDirection();
            if (blockedSide.x != 0f) d.x = -blockedSide.x * Mathf.Abs(d.x);
            if (blockedSide.y != 0f) d.y = -blockedSide.y * Mathf.Abs(d.y);
            SetHeading(d);
        }

        void SetHeading(Vector2 direction)
        {
            heading = direction;
            nextTurnTime = Time.time + Random.Range(wanderer.TurnIntervalMin, wanderer.TurnIntervalMax);
        }

        static Vector2 RandomDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
