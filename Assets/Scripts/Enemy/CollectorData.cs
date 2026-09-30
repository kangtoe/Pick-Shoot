using UnityEngine;

namespace PickShoot
{
    // 수집형 적의 데이터 (Enemies §6). 방랑형의 이동 데이터(찾아갈 탄환이 없을 때의 경로)에 탄환을 찾아가는 거리, 끄는 반경·속도, 곁에 둘 때의 간격을 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Collector Data", fileName = "CollectorData")]
    public class CollectorData : WandererData
    {
        [SerializeField, Min(0f)] float seekRadius = 12f;        // 이 거리 안의 흩어진 탄환을 찾아간다. 없으면 방랑형처럼 움직인다
        [SerializeField, Min(0f)] float pullRadius = 3.5f;       // 이 반경 안의 정착 탄환을 끈다
        [SerializeField, Min(0f)] float holdRadius = 0.9f;       // 끌어온 탄환을 두는 고리의 최소 반지름. 탄환이 많으면 간격이 지켜지도록 커진다
        [SerializeField, Min(0f)] float pullSpeed = 8f;          // 끄는 속도. 이동 속도보다 빨라야 따라온다
        [SerializeField, Min(0.01f)] float bulletSpacing = 0.5f; // 끌어온 탄환끼리 지키는 최소 간격

        public float SeekRadius => seekRadius;
        public float PullRadius => pullRadius;
        public float HoldRadius => holdRadius;
        public float PullSpeed => pullSpeed;
        public float BulletSpacing => bulletSpacing;
    }
}
