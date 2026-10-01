using UnityEngine;

namespace PickShoot
{
    // 방랑 이동의 데이터 (Enemies §6). EnemyData의 HP·이동 속도에 경로가 지나는 지점을 플레이어 주변 어디까지에서 뽑을지를 더한다.
    // 이 이동만 하는 적은 없으므로 자산은 만들지 않는다 — 수집형(CollectorData)·소환사형(SummonerData)의 데이터가 물려받는다.
    public abstract class WandererData : EnemyData
    {
        [SerializeField, Min(0f)] float crossingSpread = 1.5f; // 경로가 지나는 지점을 플레이어 주변 이 반경 안에서 뽑는다. 0이면 늘 플레이어를 정확히 겨눈다

        public float CrossingSpread => crossingSpread;
    }
}
