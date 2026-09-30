using UnityEngine;

namespace PickShoot
{
    // 수집형 적의 데이터 (Enemies §6). 방랑형의 이동 데이터에 탄환을 끄는 반경·속도를 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Collector Data", fileName = "CollectorData")]
    public class CollectorData : WandererData
    {
        [SerializeField, Min(0f)] float pullRadius = 3.5f; // 이 반경 안의 정착 탄환을 끈다
        [SerializeField, Min(0f)] float holdRadius = 0.9f; // 이 반경 안에 들어온 탄환은 놓아 둔다 — 정착 감속이 멈춰 세운다
        [SerializeField, Min(0f)] float pullSpeed = 8f;    // 끄는 속도. 이동 속도보다 빨라야 따라온다

        public float PullRadius => pullRadius;
        public float HoldRadius => holdRadius;
        public float PullSpeed => pullSpeed;
    }
}
