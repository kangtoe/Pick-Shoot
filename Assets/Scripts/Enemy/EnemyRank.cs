using UnityEngine;

namespace PickShoot
{
    // 웨이브 스폰 적의 등급 (Enemies §7, 2026-10-14) — 같은 적 종류를 세 단계로 나눠 강하게 하고, 그와 별개로 크기를 키운 챔피언을 둔다.
    // WaveManager가 스폰 순간 웨이브 번호로 단계·챔피언을 뽑아 이 묶음을 만들고 EnemyHealth.ApplyRank로 입힌다. 배율·틴트의 SSOT는 WaveManager의 직렬화 필드다.
    // 보스(서펜트)·소환 졸개·디버그 스폰은 Normal 그대로다.
    public readonly struct EnemyRank
    {
        public readonly int Tier;              // 1~3
        public readonly bool Champion;
        public readonly float HpMultiplier;
        public readonly float SpeedMultiplier;
        public readonly int DamageBonus;
        public readonly float ExpMultiplier;
        // 렌더러 색에 곱한다 — 시트의 흰 테두리가 이 색이 되어 단계가 읽힌다. 피격 점멸(SpriteFlash)은 렌더러 색을 읽지 않아 그대로 흰색이다.
        public readonly Color Tint;
        public readonly float Scale;

        public EnemyRank(int tier, bool champion, float hpMultiplier, float speedMultiplier, int damageBonus, float expMultiplier, Color tint, float scale)
        {
            Tier = tier;
            Champion = champion;
            HpMultiplier = hpMultiplier;
            SpeedMultiplier = speedMultiplier;
            DamageBonus = damageBonus;
            ExpMultiplier = expMultiplier;
            Tint = tint;
            Scale = scale;
        }

        public static readonly EnemyRank Normal = new EnemyRank(1, false, 1f, 1f, 0, 1f, Color.white, 1f);
    }
}
