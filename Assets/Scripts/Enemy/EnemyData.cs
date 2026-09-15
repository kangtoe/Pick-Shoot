using UnityEngine;

namespace PickShoot
{
    [CreateAssetMenu(menuName = "PickShoot/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] string displayName = "Enemy";
        [SerializeField, Min(1)] int maxHp = 3;
        [SerializeField, Min(0f)] float moveSpeed = 2.5f;

        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
    }
}
