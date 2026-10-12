using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PickShoot
{
    // 무기·유물 타일(ItemTilePainter)의 배지 문구와 카드(UpgradeCardPainter — 선택 카드·보기 카드)의 스탯 값과 특성 줄 — 자산 값에서 만든다 (2026-10-06).
    // 시트 글꼴 문자(A-Z 0-9 % + - ! ( ) / . 공백)만 쓴다. 없는 문자는 PixelGui가 그릴 때 예외로 알린다.
    // 2026-10-12까지는 타일 툴팁(TooltipPainter)의 사실 항목(`DMG 15`)·설명 문단도 여기서 만들었다 — 보유 장비 호버가 카드 꼴의 보기 카드로 바뀌며 걷었다.
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

        // 카드의 2 스탯: 공격력(산탄이면 X발수 — 한 번의 사격에 나가는 탄환 수)과 최대 탄약. 모두 정수 표기. 점사 수는 특성 줄(BURST)이 말한다.
        // 공격력은 플레이어 전역 배율(유물)을 곱한 실제 값 — 유물을 얻은 뒤의 무기 카드는 그만큼 큰 수를 보인다.
        // RPM(분당 사격 차례 수)은 2026-10-07 뺐다가 2026-10-12 FIRE RATE로 특성 줄 맨 앞에 돌아왔다 (WeaponTraitStats, Weapons §3).
        public static readonly string[] WeaponStatLabels = { "DMG", "AMMO" };
        // FIRE RATE 값 = 분당 사격 수 ÷ 이 값 (2026-10-12 — 같은 날 분당 수 그대로였다가 1/10로).
        const float FireRateDivisor = 10f;

        public static string[] WeaponStatValues(WeaponData d, int level, float damageMultiplier)
        {
            int dmg = Weapon.DamageAtLevel(d, level, damageMultiplier);
            int bullets = Weapon.BulletsPerShotAtLevel(d, level);
            string dmgText = bullets > 1 ? $"{dmg}X{bullets}" : dmg.ToString();
            return new[] { dmgText, Weapon.MaxAmmoAtLevel(d, level).ToString() };
        }

        // 무기의 **수치** 특성 — 모든 무기의 발사 속도(`FIRE RATE 30`, 분당 사격 수의 1/10을 반올림한 정수, 단위 없음) 뒤에 그 무기에만 있는 점사·튕김·관통·폭발 반경.
        // 발사 속도 말고는 0이거나 기본인 것을 넣지 않아 무기를 가르는 것만 남는다. 발사 속도는 자루마다 제 쿨다운대로 쏘게 되며(2026-10-12, Weapons §1)
        // 곧 그 자루의 화력 기여라 모든 무기에 적는다 — 레벨로 바뀌지 않으므로 DMG·AMMO 표가 아니라 값 하나로 서는 이 줄들에 둔다.
        // 점사 안의 사격도 하나씩 센다(어썰트 라이플 24 — 차례로 세면 8). 같은 날 초당 차례 수 `5/S`(소수 한 자리) → 분당 정수 `300` → 그 1/10로 바꿨다 —
        // `5/S`는 어썰트 라이플이 `1.4/S`로 런처 다음으로 느려 보였고, 정수는 소수점이 없고 쿨다운 초로 읽힐 일도 없어 단위를 뺐다.
        // 카드에서는 DMG·AMMO와 같은 표의 줄이다 (2026-10-07 — 결국 같은 성격의 수치라 따로 둘 이유가 없다).
        // 적지 않는 것 — 산탄의 발수·각도(DMG의 `8X5`가 이미 말한다), 고정 탄창(`AMMO 1 -> 1`이 이미 말한다). 전부 2026-10-07에 뺐다.
        // 탄속·넉백도 적지 않는다 (2026-10-12) — 레벨·개조로 바뀌지 않고 수만으로는 빠른지 알 수 없어 고르는 근거가 못 된다. 타일 툴팁에만 있다가 툴팁과 함께 걷었다.
        // 레벨에 따라 바뀌는 것은 점사 수와 그에 따른 발사 속도뿐이다 (탄창이 빌 때까지 쏘는 블래스터) — 카드는 그 줄만 "현재 → 다음"으로 보인다.
        // 개조(mod, 없으면 null)가 더한 튕김·관통·폭발 반경은 자산 값과 합친 값이다 — 따로 적지 않는다 (Mods §1).
        public static void WeaponTraitStats(WeaponData d, ModData mod, int level, List<string> labels, List<string> values)
        {
            labels.Add("FIRE RATE");
            // 반올림은 .5를 올린다 (산탄총 7.5 → 8) — Mathf.RoundToInt는 짝수 쪽으로 붙어 6.5가 6이 된다.
            values.Add(Mathf.FloorToInt(Weapon.ShotsPerMinuteAtLevel(d, level) / FireRateDivisor + 0.5f).ToString());
            int burst = Weapon.ShotsPerTriggerAtLevel(d, level);
            if (burst > 1) { labels.Add("BURST"); values.Add(burst.ToString()); }
            int bounces = Weapon.BouncesWith(d, mod);
            if (bounces > 0) { labels.Add("BOUNCE"); values.Add(bounces.ToString()); }
            int pierce = Weapon.PenetrationWith(d, mod);
            if (pierce > 0) { labels.Add("PIERCE"); values.Add(pierce.ToString()); }
            float blast = Weapon.ExplosionRadiusWith(d, mod);
            if (blast > 0f) { labels.Add("BLAST"); values.Add(Num(blast)); }
        }

        // 수치가 없는 특성 — 표에 둘 값이 없어 카드에서는 표 아래 한 줄이 된다.
        public static void WeaponTraitFlags(WeaponData d, List<string> flags)
        {
            if (d.ExplosionRadius > 0f && d.ExplosionPullsInward) flags.Add("PULLS IN");
            if (d.AutoRetrieve) flags.Add("AUTO RETRIEVE");
        }

        // 소수는 한 자리까지, 정수는 소수점 없이 (14 · 6.5). 소수점은 글꼴에 있다 (DEVELOPMENT §2). 문화권과 무관하게 '.'.
        static string Num(float v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
