using UnityEngine;

namespace PickShoot
{
    // 방랑형 적의 데이터 (Enemies §6). EnemyData의 HP·이동 속도에 방향을 바꾸기까지의 시간 범위를 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Wanderer Data", fileName = "WandererData")]
    public class WandererData : EnemyData
    {
        [SerializeField, Min(0f)] float turnIntervalMin = 1.5f; // 한 방향으로 나아가는 최소 시간
        [SerializeField, Min(0f)] float turnIntervalMax = 3.5f; // 최대 시간. 방향을 바꿀 때마다 사이에서 무작위로 뽑는다

        public float TurnIntervalMin => turnIntervalMin;
        public float TurnIntervalMax => turnIntervalMax;
    }
}
