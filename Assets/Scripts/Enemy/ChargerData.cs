using UnityEngine;

namespace PickShoot
{
    // 돌진형 적의 데이터 (Enemies §4). EnemyData의 HP·이동 속도(접근 속도)에 돌진 사이클의 거리·시간·속도를 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Charger Data", fileName = "ChargerData")]
    public class ChargerData : EnemyData
    {
        [SerializeField, Min(0f)] float chargeTriggerDistance = 6f; // 플레이어가 이 거리 안에 들어오면 준비 시작
        [SerializeField, Min(0f)] float windupDuration = 0.7f;      // 준비(정지·깜빡임) 시간. 플레이어가 비킬 여유
        [SerializeField, Min(0f)] float chargeSpeed = 14f;          // 돌진 속도
        [SerializeField, Min(0f)] float chargeMaxDistance = 9f;     // 벽·플레이어에 닿지 않아도 이만큼 달리면 끝
        [SerializeField, Min(0f)] float recoveryDuration = 0.6f;    // 돌진 뒤 멈춰 있는 시간

        public float ChargeTriggerDistance => chargeTriggerDistance;
        public float WindupDuration => windupDuration;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeMaxDistance => chargeMaxDistance;
        public float RecoveryDuration => recoveryDuration;
    }
}
