using System.Collections.Generic;
using System.Globalization;

namespace PickShoot
{
    // 무기·유물 타일(ItemTilePainter)의 배지 문구와 툴팁(TooltipPainter) 내용, 카드(UpgradeCardPainter)의 스탯 값과 특성 줄 — 자산 값에서 만든다 (2026-10-06).
    // 시트 글꼴 문자(A-Z 0-9 % + - ! ( ) / . 공백)만 쓴다. 없는 문자는 PixelGui가 그릴 때 예외로 알린다.
    // 툴팁 내용은 제목(이름) + 사실 묶음(facts: `DMG 15`처럼 짧은 항목 — 한 항목은 줄을 나누지 않고 TooltipPainter가 폭에 맞춰 여러 항목을 한 줄에 채운다)
    // + 설명 문단(notes: 자산의 description — 단어 단위로 접는다). 무기 설명은 아직 자산에 비어 있다 — 특성은 수치에서 만든 특성 항목(WeaponTraits)이 알린다.
    public static class ItemInfoText
    {
        // 무기 타일의 레벨 배지 `LV5` — HUD·카드의 `LV 5`와 같은 낱말이고, 좁은 타일 띠에서는 띄어쓰기 없이 붙인다 (2026-10-06, 그전 `L5`. `LV.5`도 잠깐
        // 썼다가 점을 뺐다).
        public static string WeaponBadge(int level)
        {
            return "LV" + level;
        }

        // 개조된 자루의 배지 머리글자 — 레벨 배지 뒤에 개조 색으로 붙는다 (`LV5H`, ItemTilePainter). 개조가 없으면 null.
        public static string ModMark(ModData mod)
        {
            return mod != null ? mod.BadgeMark : null;
        }

        public static string RelicBadge(int count)
        {
            return "X" + count;
        }

        // 카드·툴팁의 2 스탯: 공격력(산탄이면 X발수 — 한 번의 사격에 나가는 탄환 수)과 최대 탄약. 모두 정수 표기. 점사 수는 특성 줄(BURST)이 말한다.
        // 공격력은 플레이어 전역 배율(유물)을 곱한 실제 값 — 유물을 얻은 뒤의 무기 카드는 그만큼 큰 수를 보인다.
        // RPM(분당 사격 차례 수)은 2026-10-07 뺐다 — 카드의 그 줄을 특성 줄(WeaponTraits)에 내줬다 (Weapons §3).
        public static readonly string[] WeaponStatLabels = { "DMG", "AMMO" };

        public static string[] WeaponStatValues(WeaponData d, int level, float damageMultiplier)
        {
            int dmg = Weapon.DamageAtLevel(d, level, damageMultiplier);
            int bullets = Weapon.BulletsPerShotAtLevel(d, level);
            string dmgText = bullets > 1 ? $"{dmg}X{bullets}" : dmg.ToString();
            return new[] { dmgText, Weapon.MaxAmmoAtLevel(d, level).ToString() };
        }

        public static string WeaponTitle(WeaponData d)
        {
            return d.DisplayName.ToUpperInvariant();
        }

        // 무기 한 자루의 사실 항목 — 레벨, 개조(`MOD HOMING`), 2 스탯, 탄속·넉백, 그리고 특성(수치 특성 + 깃발 특성).
        // 슬롯 번호는 적지 않는다 (2026-10-07) — 자루를 가리키는 데 쓰였지만 카드·타일의 그림과 레벨로 충분하고, 줄만 차지했다.
        // mod는 그 자루의 개조(없으면 null) — 개조가 더한 수치는 특성 항목에 합친 값(`PIERCE 3`)으로 들어간다 (Mods §4.2).
        public static void WeaponFacts(WeaponData d, ModData mod, int level, PlayerStats stats, List<string> facts)
        {
            facts.Add($"LEVEL {level}");
            if (mod != null) facts.Add($"MOD {mod.UiName}");
            var values = WeaponStatValues(d, level, stats.DamageMultiplier);
            for (int i = 0; i < values.Length; i++)
                facts.Add($"{WeaponStatLabels[i]} {values[i]}");
            facts.Add($"SPEED {Num(d.BulletSpeed)}");
            facts.Add($"KNOCKBACK {Num(d.KnockbackForce)}");
            var traitLabels = new List<string>();
            var traitValues = new List<string>();
            WeaponTraitStats(d, mod, level, traitLabels, traitValues);
            for (int i = 0; i < traitLabels.Count; i++)
                facts.Add($"{traitLabels[i]} {traitValues[i]}");
            WeaponTraitFlags(d, facts);
        }

        // 그 무기에만 있는 **수치** 특성 — 점사·튕김·관통·폭발 반경. 0이거나 기본인 것은 넣지 않아 무기를 가르는 것만 남는다.
        // 카드에서는 DMG·AMMO와 같은 표의 줄이고(2026-10-07 — 결국 같은 성격의 수치라 따로 둘 이유가 없다), 툴팁에서는 `PIERCE 1` 항목이다.
        // 적지 않는 것 — 산탄의 발수·각도(DMG의 `8X5`가 이미 말한다), 고정 탄창(`AMMO 1 -> 1`이 이미 말한다). 전부 2026-10-07에 뺐다.
        // 레벨에 따라 바뀌는 것은 점사 수뿐이다 (탄창이 빌 때까지 쏘는 블래스터) — 카드는 그 줄만 "현재 → 다음"으로 보인다.
        // 개조(mod, 없으면 null)가 더한 튕김·관통·폭발 반경은 자산 값과 합친 값이다 — 따로 적지 않는다 (Mods §1).
        public static void WeaponTraitStats(WeaponData d, ModData mod, int level, List<string> labels, List<string> values)
        {
            int burst = Weapon.ShotsPerTriggerAtLevel(d, level);
            if (burst > 1) { labels.Add("BURST"); values.Add(burst.ToString()); }
            int bounces = Weapon.BouncesWith(d, mod);
            if (bounces > 0) { labels.Add("BOUNCE"); values.Add(bounces.ToString()); }
            int pierce = Weapon.PenetrationWith(d, mod);
            if (pierce > 0) { labels.Add("PIERCE"); values.Add(pierce.ToString()); }
            float blast = Weapon.ExplosionRadiusWith(d, mod);
            if (blast > 0f) { labels.Add("BLAST"); values.Add(Num(blast)); }
        }

        // 수치가 없는 특성 — 표에 둘 값이 없어 카드에서는 표 아래 한 줄, 툴팁에서는 항목 하나가 된다.
        public static void WeaponTraitFlags(WeaponData d, List<string> flags)
        {
            if (d.ExplosionRadius > 0f && d.ExplosionPullsInward) flags.Add("PULLS IN");
            if (d.AutoRetrieve) flags.Add("AUTO RETRIEVE");
        }

        public static void WeaponNotes(WeaponData d, ModData mod, List<string> notes)
        {
            if (!string.IsNullOrEmpty(d.Description)) notes.Add(d.Description);
            if (mod != null && !string.IsNullOrEmpty(mod.Description)) notes.Add(mod.Description);
        }

        public static string RelicTitle(RelicData d)
        {
            return d.DisplayName.ToUpperInvariant();
        }

        // 유물 한 종류의 사실 항목: 등급과 그 보유분이 더하는 수정치(수정치 × 개수 — 종류별 총계가 아니라 이 유물 몫).
        // 보유 수(`OWNED 3`)는 적지 않는다 (2026-10-07) — 타일 배지의 `X3`이 바로 옆에서 이미 말한다.
        public static void RelicFacts(RelicData d, int count, List<string> facts)
        {
            facts.Add(d.Grade.ToString().ToUpperInvariant());
            foreach (var s in PlayerStatModifiers.Stats)
            {
                int v = d.Modifiers.Get(s);
                if (v == 0) continue;
                facts.Add($"{PlayerStatModifiers.Label(s)} {PlayerStatModifiers.FormatSigned(s, v * count)}");
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
