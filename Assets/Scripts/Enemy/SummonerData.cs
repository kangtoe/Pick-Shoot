using UnityEngine;

namespace PickShoot
{
    // 소환사형 적의 데이터 (Enemies §6). 방랑 이동 데이터(HP·이동 속도·경로 반경)에 쏘는 졸개의 주기와 상한, 죽을 때 놓는 일반 졸개의 수를 더한다.
    [CreateAssetMenu(menuName = "PickShoot/Summoner Data", fileName = "SummonerData")]
    public class SummonerData : WandererData
    {
        [SerializeField, Min(0.1f)] float summonInterval = 2f; // 이 주기마다 쏘는 졸개 하나를 예고한다. 스폰 뒤 첫 예고도 한 주기 뒤
        [SerializeField, Min(1)] int maxDarts = 4;             // 살아 있는·예고 중인 쏘는 졸개가 이만큼이면 그 주기는 건너뛴다 (소환사마다 따로 셈). 스스로 금방 죽어 안전장치에 가깝다
        [SerializeField, Min(0)] int deathMinions = 3;         // 플레이어에게 죽을 때 그 자리 둘레에 놓는 일반 졸개 수. 0이면 놓지 않는다

        public float SummonInterval => summonInterval;
        public int MaxDarts => maxDarts;
        public int DeathMinions => deathMinions;
    }
}
