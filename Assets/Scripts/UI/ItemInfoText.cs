using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PickShoot
{
    // 무기·유물 타일(ItemTilePainter)의 배지 문구와 툴팁(TooltipPainter) 내용, 카드(UpgradeCardPainter)의 스탯 값 — 자산 값에서 만든다 (2026-10-06).
    // 시트 글꼴 문자(A-Z 0-9 % + - ! ( ) / . 공백)만 쓴다. 없는 문자는 PixelGui가 그릴 때 예외로 알린다.
    // 툴팁 내용은 제목(이름) + 사실 묶음(facts: `DMG 15`처럼 짧은 항목 — 한 항목은 줄을 나누지 않고 TooltipPainter가 폭에 맞춰 여러 항목을 한 줄에 채운다)
    // + 설명 문단(notes: 자산의 description — 단어 단위로 접는다). 무기 설명은 아직 자산에 비어 있다 — 특성은 수치에서 만든 사실 항목이 알린다.
    public static class ItemInfoText
    {
        public static string WeaponBadge(int level)
        {
            return "L" + level;
        }

        public static string RelicBadge(int count)
        {
            return "X" + count;
        }

        // 카드·툴팁의 3 스탯: 공격력(한 차례에 여러 발이면 X발수 — 동시 발사 × 점사), 분당 사격 차례 수(RPM, 점사 소요 시간 포함), 최대 탄약. 모두 정수 표기.
        // 공격력은 플레이어 전역 배율(유물)을 곱한 실제 값 — 유물을 얻은 뒤의 무기 카드는 그만큼 큰 수를 보인다.
        public static readonly string[] WeaponStatLabels = { "DMG", "RPM", "AMMO" };

        public static string[] WeaponStatValues(WeaponData d, int level, float damageMultiplier)
        {
            int dmg = Weapon.DamageAtLevel(d, level, damageMultiplier);
            int bullets = Weapon.BulletsPerTriggerAtLevel(d, level);
            string dmgText = bullets > 1 ? $"{dmg}X{bullets}" : dmg.ToString();
            int rpm = Mathf.RoundToInt(60f / Weapon.TriggerIntervalAtLevel(d, level));
            return new[] { dmgText, rpm.ToString(), Weapon.MaxAmmoAtLevel(d, level).ToString() };
        }

        public static string WeaponTitle(WeaponData d)
        {
            return d.DisplayName.ToUpperInvariant();
        }

        // 무기 한 자루의 사실 항목. slotIndex는 보유 자루의 슬롯(0부터), 획득 카드처럼 아직 슬롯이 없으면 -1 (`NEW`).
        // 3 스탯 뒤에 탄속·넉백, 그리고 그 무기에만 있는 특성(산탄·점사·튕김·관통·폭발·자동 회수·고정 탄창) — 0이거나 기본인 것은 적지 않아 무기를 가르는 것만 남는다.
        public static void WeaponFacts(WeaponData d, int level, int slotIndex, PlayerStats stats, List<string> facts)
        {
            facts.Add(slotIndex >= 0 ? $"SLOT {slotIndex + 1}" : "NEW");
            facts.Add($"LEVEL {level}");
            var values = WeaponStatValues(d, level, stats.DamageMultiplier);
            for (int i = 0; i < values.Length; i++)
                facts.Add($"{WeaponStatLabels[i]} {values[i]}");
            facts.Add($"SPEED {Num(d.BulletSpeed)}");
            facts.Add($"KNOCKBACK {Num(d.KnockbackForce)}");
            if (d.ProjectilesPerShot > 1) facts.Add($"X{d.ProjectilesPerShot} SPREAD {Num(d.SpreadAngleDegrees)}");
            int burst = Weapon.ShotsPerTriggerAtLevel(d, level);
            if (burst > 1) facts.Add($"BURST {burst}");
            if (d.MaxBounces > 0) facts.Add($"BOUNCE {d.MaxBounces}");
            if (d.Penetration > 0) facts.Add($"PIERCE {d.Penetration}");
            if (d.ExplosionRadius > 0f) facts.Add(d.ExplosionPullsInward ? $"BLAST {Num(d.ExplosionRadius)} PULLS IN" : $"BLAST {Num(d.ExplosionRadius)}");
            if (d.AutoRetrieve) facts.Add("AUTO RETRIEVE");
            if (d.LevelMaxAmmoAdd == 0) facts.Add("FIXED AMMO");
        }

        public static void WeaponNotes(WeaponData d, List<string> notes)
        {
            if (!string.IsNullOrEmpty(d.Description)) notes.Add(d.Description);
        }

        public static string RelicTitle(RelicData d)
        {
            return d.DisplayName.ToUpperInvariant();
        }

        // 유물 한 종류의 사실 항목: 보유 수, 등급, 그 보유분이 더하는 수정치(수정치 × 개수 — 종류별 총계가 아니라 이 유물 몫).
        public static void RelicFacts(RelicData d, int count, List<string> facts)
        {
            facts.Add($"OWNED {count}");
            facts.Add(d.Grade.ToString().ToUpperInvariant());
            foreach (var s in PlayerStatModifiers.Stats)
            {
                int v = d.Modifiers.Get(s);
                if (v == 0) continue;
                facts.Add($"{PlayerStatModifiers.Label(s)} {PlayerStatModifiers.Format(s, v * count)}");
            }
        }

        public static void RelicNotes(RelicData d, List<string> notes)
        {
            if (!string.IsNullOrEmpty(d.Description)) notes.Add(d.Description);
        }

        // 소수는 한 자리까지, 정수는 소수점 없이 (14 · 6.5). 소수점은 글꼴에 있다 (DEVELOPMENT §2). 문화권과 무관하게 '.'.
        static string Num(float v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
