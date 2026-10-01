using UnityEngine;

namespace PickShoot
{
    // 수집형 적의 데이터 (Enemies §6). 방랑 이동 데이터(찾아갈 탄환이 없을 때의 경로)에 끄는 반경·속도, 곁에 둘 때의 간격, 든 만큼 느려지는 정도를 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Collector Data", fileName = "CollectorData")]
    public class CollectorData : WandererData
    {
        [SerializeField, Min(0f)] float pullRadius = 1.5f;       // 이 반경 안의 정착 탄환을 끈다. 몸 가까이라 걸어가서 가져간다
        [SerializeField, Min(0f)] float holdRadius = 0.9f;       // 끌어온 탄환을 두는 고리의 최소 반지름. 탄환이 많으면 간격이 지켜지도록 커진다
        [SerializeField, Min(0f)] float pullSpeed = 8f;          // 끄는 속도. 이동 속도보다 빨라야 따라온다
        [SerializeField, Min(0.01f)] float bulletSpacing = 0.5f; // 끌어온 탄환끼리 지키는 최소 간격
        [SerializeField, Min(1)] int slowdownBullets = 20;       // 이만큼 들면 가장 느리다. 사이는 든 수에 선형
        [SerializeField, Range(0f, 1f)] float loadedSpeedMultiplier = 0.3f; // 가장 느릴 때의 이동 속도 배율

        public float PullRadius => pullRadius;
        public float HoldRadius => holdRadius;
        public float PullSpeed => pullSpeed;
        public float BulletSpacing => bulletSpacing;
        public int SlowdownBullets => slowdownBullets;
        public float LoadedSpeedMultiplier => loadedSpeedMultiplier;
    }
}
