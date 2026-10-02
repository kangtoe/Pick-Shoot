using UnityEngine;

namespace PickShoot
{
    [CreateAssetMenu(menuName = "PickShoot/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] string displayName = "Enemy";
        [SerializeField, Min(1)] int maxHp = 30;
        // 플레이어가 죽였을 때 적립되는 스코어 (GDD §3.3). 웨이브 종료 정리·졸개의 벽 자멸로 죽는 것은 적립하지 않는다. 0이면 보상 없는 적.
        [SerializeField, Min(0)] int scoreValue = 10;
        [SerializeField, Min(0f)] float moveSpeed = 2.5f;

        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public int ScoreValue => scoreValue;
        public float MoveSpeed => moveSpeed;
    }
}
