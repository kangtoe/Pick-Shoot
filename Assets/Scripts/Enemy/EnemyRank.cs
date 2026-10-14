using UnityEngine;

namespace PickShoot
{
    // 웨이브 스폰 적의 등급 (Enemies §7, 2026-10-14) — 같은 적 종류를 세 단계로 나눠 강하게 하고, 그와 별개로 크기를 키운 챔피언을 둔다.
    // WaveManager가 스폰 순간 웨이브 번호로 단계·챔피언을 뽑아 이 묶음을 만들고 EnemyHealth.ApplyRank로 입힌다. 배율의 SSOT는 WaveManager의 직렬화 필드다.
    // 보스(서펜트)는 단계만 받고 챔피언이 되지 않는다. 서펜트 몸통·소환 졸개는 낳은 쪽의 단계를 물려받고, 디버그 스폰은 Normal 그대로다.
    public readonly struct EnemyRank
    {
        public readonly int Tier;              // 1~3
        public readonly bool Champion;
        public readonly float HpMultiplier;
        public readonly float SpeedMultiplier;
        public readonly int DamageBonus;
        public readonly float ExpMultiplier;
        // 2·3단계가 갈아 끼울 몸 색 사본의 짝. 1단계(Normal)는 원본 그림 그대로라 null이다.
        public readonly EnemyTierArt Art;
        public readonly float Scale;

        public EnemyRank(int tier, bool champion, float hpMultiplier, float speedMultiplier, int damageBonus, float expMultiplier, EnemyTierArt art, float scale)
        {
            Tier = tier;
            Champion = champion;
            HpMultiplier = hpMultiplier;
            SpeedMultiplier = speedMultiplier;
            DamageBonus = damageBonus;
            ExpMultiplier = expMultiplier;
            Art = art;
            Scale = scale;
        }

        public static readonly EnemyRank Normal = new EnemyRank(1, false, 1f, 1f, 0, 1f, null, 1f);
    }
}
