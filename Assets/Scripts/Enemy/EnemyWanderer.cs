using UnityEngine;

namespace PickShoot
{
    // 방랑형 적 (Enemies §6). 플레이어 근처를 가로지르는 직선 경로를 벽에 닿을 때까지 가고, 닿으면 그 자리에서 다시 플레이어 근처를 지나는 경로를 잡는다.
    //   경로: 플레이어 위치 주변(데이터의 반경 안 무작위 지점)을 향하는 방향. 플레이어를 지나쳐도 멈추거나 되돌지 않고 벽까지 간다 — 쫓는 것이 아니라 가로지르는 것.
    //   벽: 공통 골격이 막힌 쪽을 알려 주면 새 경로를 잡는다. 새 경로가 다시 그 벽을 향하면(플레이어가 같은 벽에 붙어 있을 때) 그 축의 부호만 뒤집어 벽에서
    //       떨어져 나간다. 진행 방향이 이미 안쪽인데 넉백·분리 힘에 밀려 막힌 것이면 그대로 둔다 — 벽에 눌린 몇 스텝 동안 매번 경로가 바뀌지 않게.
    //   플레이어가 없으면(사망) 가던 방향을 유지하고 벽에서는 아무 안쪽 방향이나 잡는다.
    // 바라보는 방향은 플레이어가 아니라 진행 방향이다 — 어디로 가는지 읽혀야 피할 수 있다. 상하로만 갈 때는 마지막 좌우를 유지한다(Face의 데드존).
    // 넉백·분리 힘·벽 판정·걷기는 공통 골격 그대로. 수집형(EnemyCollector)은 이 이동을 물려받아 찾아갈 탄환이 없을 때 쓴다.
    public class EnemyWanderer : EnemyMover
    {
        WandererData wanderer;
        Vector2 heading;

        protected override void Awake()
        {
            base.Awake();
            wanderer = Data as WandererData;
            if (wanderer == null)
                throw new System.Exception($"[PickShoot] {GetType().Name}.data가 WandererData 자산이 아님 ({name}: {Data.name}).");
        }

        // 플레이어는 공통 골격의 Start에서 잡히므로 첫 경로도 그 뒤에 잡는다.
        protected override void Start()
        {
            base.Start();
            TakeNewPath();
        }

        protected override Vector2 Steer() => SteerAlongPath();

        // 경로를 따라 나아가는 이 스텝의 속도. 다른 행동을 겸하는 적(수집형)이 경로 이동으로 돌아올 때 부른다.
        protected Vector2 SteerAlongPath()
        {
            Face(heading.x);
            return heading * Data.MoveSpeed;
        }

        // 지금 자리에서 플레이어 근처를 지나는 새 경로를 잡는다.
        protected void TakeNewPath() => heading = PathHeading();

        // 진행 방향이 막힌 쪽을 향할 때만 새 경로를 잡는다. 새 경로가 막힌 축에서 다시 벽을 향하면 그 축만 뒤집는다 — 구석이면 두 축 모두.
        protected override void OnWallBlocked(Vector2 blockedSide)
        {
            bool intoX = heading.x * blockedSide.x > 0f;
            bool intoY = heading.y * blockedSide.y > 0f;
            if (!intoX && !intoY) return;

            Vector2 d = PathHeading();
            if (d.x * blockedSide.x > 0f) d.x = -d.x;
            if (d.y * blockedSide.y > 0f) d.y = -d.y;
            heading = d;
        }

        // 플레이어 주변의 무작위 지점을 향하는 단위 방향. 플레이어가 없거나 그 지점에 겹쳐 있으면 아무 방향.
        Vector2 PathHeading()
        {
            if (Target != null)
            {
                Vector2 point = (Vector2)Target.position + Random.insideUnitCircle * wanderer.CrossingSpread;
                Vector2 delta = point - Body.position;
                if (delta.sqrMagnitude > 0.0001f) return delta.normalized;
            }
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
