using UnityEngine;

namespace PickShoot
{
    // 서펜트형 보스의 데이터 (Enemies §5). EnemyData의 HP는 마디 하나의 HP(머리·몸통 같음), 이동 속도는 머리의 속도다.
    [CreateAssetMenu(menuName = "PickShoot/Serpent Data", fileName = "SerpentData")]
    public class SerpentData : EnemyData
    {
        [SerializeField, Min(1)] int segmentCount = 8;            // 스폰 시 머리 뒤에 붙는 몸통 마디 수
        [SerializeField, Min(0.1f)] float segmentSpacing = 1.25f; // 이웃한 마디 중심 사이의 궤적 거리 (월드 유닛)
        [SerializeField, Min(0f)] float turnRate = 150f;          // 머리가 방향을 트는 최대 속도 (도/초). 낮을수록 크게 돈다

        public int SegmentCount => segmentCount;
        public float SegmentSpacing => segmentSpacing;
        public float TurnRate => turnRate;
    }
}
