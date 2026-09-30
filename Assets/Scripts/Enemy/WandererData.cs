using UnityEngine;

namespace PickShoot
{
    // 방랑형 적의 데이터 (Enemies §6). EnemyData의 HP·이동 속도에 경로가 지나는 지점을 플레이어 주변 어디까지에서 뽑을지를 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Wanderer Data", fileName = "WandererData")]
    public class WandererData : EnemyData
    {
        [SerializeField, Min(0f)] float crossingSpread = 1.5f; // 경로가 지나는 지점을 플레이어 주변 이 반경 안에서 뽑는다. 0이면 늘 플레이어를 정확히 겨눈다

        public float CrossingSpread => crossingSpread;
    }
}
