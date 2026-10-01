using UnityEngine;

namespace PickShoot
{
    // 방랑 이동의 데이터 (Enemies §6). EnemyData의 HP·이동 속도에 경로가 향하는 지점을 화면 가장자리에서 얼마나 떨어진 안쪽에서 뽑을지를 더한다.
    // 이 이동만 하는 적은 없으므로 자산은 만들지 않는다 — 수집형(CollectorData)·소환사형(SummonerData)의 데이터가 물려받는다.
    public abstract class WandererData : EnemyData
    {
        [SerializeField, Min(0f)] float edgeMargin = 2f; // 경로가 향하는 지점을 화면에 보이는 영역의 가장자리에서 이만큼 안쪽에서 뽑는다. 카메라 반높이(8.4375)보다 작아야 한다

        public float EdgeMargin => edgeMargin;
    }
}
