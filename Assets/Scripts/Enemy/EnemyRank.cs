using UnityEngine;

namespace PickShoot
{
    public enum EnemyRankKind { Normal, Champion, Ultra }

    // 웨이브 스폰 적의 등급과 웨이브 강화 (Enemies §7, 2026-10-14, 등급은 2026-10-15 개편) — 노멀·챔피언·울트라는 색 없이 크기로만 읽힌다.
    // WaveManager가 예고를 띄우기 전에 그 웨이브의 확률로 등급을 뽑아 이 묶음을 만들고(예고 마커도 Scale만큼 커진다) 스폰 직후 EnemyHealth.ApplyRank로 입힌다.
    // 배율의 SSOT는 WaveManager의 직렬화 필드다. WaveMultiplier는 보이지 않는 웨이브 강화(HP·경험치)로 HpMultiplier·ExpMultiplier에 이미 곱해져 있고,
    // 낳은 적(소환 졸개)이 노멀 크기로 물려받도록 따로 들고 다닌다. 보스(서펜트)는 노멀에 웨이브 강화만, 디버그 스폰은 Normal 그대로다.
    public readonly struct EnemyRank
    {
        public readonly EnemyRankKind Kind;
        public readonly float HpMultiplier;
        public readonly float SpeedMultiplier;
        public readonly int DamageBonus;
        public readonly float ExpMultiplier;
        public readonly float Scale;
        public readonly float WaveMultiplier;

        public EnemyRank(EnemyRankKind kind, float hpMultiplier, float speedMultiplier, int damageBonus, float expMultiplier, float scale, float waveMultiplier)
        {
            Kind = kind;
            HpMultiplier = hpMultiplier;
            SpeedMultiplier = speedMultiplier;
            DamageBonus = damageBonus;
            ExpMultiplier = expMultiplier;
            Scale = scale;
            WaveMultiplier = waveMultiplier;
        }

        public static readonly EnemyRank Normal = new EnemyRank(EnemyRankKind.Normal, 1f, 1f, 0, 1f, 1f, 1f);
    }
}
