using UnityEngine;

namespace PickShoot
{
    [CreateAssetMenu(menuName = "PickShoot/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] string displayName = "Enemy";
        [SerializeField, Min(1)] int maxHp = 30;
        // 몸이 플레이어에 닿을 때 주는 피해 (Enemies §1). 플레이어 시작 체력 10 기준 기본 1.
        [SerializeField, Min(1)] int contactDamage = 1;
        // 플레이어가 죽였을 때 적립되는 경험치 (GDD §3.3). 웨이브 종료 정리·졸개의 벽 자멸로 죽는 것은 적립하지 않는다. 0이면 보상 없는 적.
        [SerializeField, Min(0)] int expValue = 1;
        [SerializeField, Min(0f)] float moveSpeed = 2.5f;

        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public int ContactDamage => contactDamage;
        public int ExpValue => expValue;
        public float MoveSpeed => moveSpeed;
    }
}
