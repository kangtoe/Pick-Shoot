using UnityEngine;

namespace PickShoot
{
    // 유물·캐릭터가 올리는 플레이어 스탯 8개 (Relics §2, Characters §3). 수정치 묶음(PlayerStatModifiers)의 칸과 카드의 줄 순서가 이 순서다.
    public enum PlayerStat
    {
        Damage,         // 공격력 배율 (%) — 모든 무기의 피해에 곱한다
        MoveSpeed,      // 이동속도 (%)
        MaxHp,          // 최대 체력 (+정수)
        Regen,          // 재생 (+정수) — 주기마다 체력 1 회복, 수치가 클수록 주기가 짧다 (PlayerHealth)
        PickupRadius,   // 탄환 회수 범위 — 자석 반경 (%)
        PickupSpeed,    // 탄환 회수 속도 — 자석이 끌어오는 속도 (%)
        Recharge,       // 능력 리차지 속도 — 대시 등 모든 능력의 쿨다운에 공통 (%)
        MaxAmmo,        // 최대 탄약 (+정수) — 모든 자루의 최대 탄약에 더한다 (Weapon). 캐릭터(HUNTER)가 쓰는 여덟째 칸 (Characters §3, 2026-10-13)
    }

    // 플레이어 스탯에 더하는 수정치 묶음. 유물 하나·캐릭터 하나가 묶음 하나를 가지고, PlayerStats가 캐릭터와 보유 유물의 묶음을 전부 더해 총계를 만든다.
    // 배율 스탯은 퍼센트 정수(10 = +10%), 최대 체력·재생·최대 탄약은 그대로 더하는 정수 — 카드 글꼴이 숫자와 %뿐이라 소수를 두지 않는다.
    // 최대 탄약이 퍼센트가 아닌 이유 (Characters §3): 퍼센트는 작은 탄창에서 반올림에 묻히고(런처 2발의 +20%는 0) 탄창이 큰 무기에서만 뜻이 생긴다.
    // 커먼 유물은 한 칸만 채우지만, 언커먼·레어 유물(Relics §3)의 "공격력↑ 이동속도↓" 같은 복합·패널티 효과도 같은 묶음으로 적는다 — 음수 허용.
    [System.Serializable]
    public struct PlayerStatModifiers
    {
        public int damagePercent;
        public int moveSpeedPercent;
        public int maxHp;
        public int regen;
        public int pickupRadiusPercent;
        public int pickupSpeedPercent;
        public int rechargePercent;
        public int maxAmmo;

        // 카드·디버그 창이 스탯을 순서대로 훑을 때 쓴다.
        public static readonly PlayerStat[] Stats = (PlayerStat[])System.Enum.GetValues(typeof(PlayerStat));

        public int Get(PlayerStat stat)
        {
            switch (stat)
            {
                case PlayerStat.Damage: return damagePercent;
                case PlayerStat.MoveSpeed: return moveSpeedPercent;
                case PlayerStat.MaxHp: return maxHp;
                case PlayerStat.Regen: return regen;
                case PlayerStat.PickupRadius: return pickupRadiusPercent;
                case PlayerStat.PickupSpeed: return pickupSpeedPercent;
                case PlayerStat.Recharge: return rechargePercent;
                case PlayerStat.MaxAmmo: return maxAmmo;
                default: throw new System.Exception($"[PickShoot] PlayerStatModifiers.Get: 알 수 없는 스탯 {stat}.");
            }
        }

        public bool IsZero
        {
            get
            {
                foreach (var s in Stats)
                    if (Get(s) != 0) return false;
                return true;
            }
        }

        public static PlayerStatModifiers operator +(PlayerStatModifiers a, PlayerStatModifiers b)
        {
            return new PlayerStatModifiers
            {
                damagePercent = a.damagePercent + b.damagePercent,
                moveSpeedPercent = a.moveSpeedPercent + b.moveSpeedPercent,
                maxHp = a.maxHp + b.maxHp,
                regen = a.regen + b.regen,
                pickupRadiusPercent = a.pickupRadiusPercent + b.pickupRadiusPercent,
                pickupSpeedPercent = a.pickupSpeedPercent + b.pickupSpeedPercent,
                rechargePercent = a.rechargePercent + b.rechargePercent,
                maxAmmo = a.maxAmmo + b.maxAmmo,
            };
        }

        // 배율 스탯(퍼센트 표기)인가, 정수 가산인가.
        public static bool IsPercent(PlayerStat stat)
        {
            return stat != PlayerStat.MaxHp && stat != PlayerStat.Regen && stat != PlayerStat.MaxAmmo;
        }

        // 카드 줄의 항목 이름. 줄임말 대신 읽어서 바로 아는 말로 둔다 (2026-10-07, 그전엔 DMG·SPEED·RANGE·PULL·SKILL 5글자) — 유물 카드가
        // 설명 문구 없이 이 줄 하나로 효과를 말하기 때문이다 (Relics §2). 폭은 가장 긴 PICKUP RANGE·PICKUP SPEED가 244px이고, 값 열에 세 자리
        // 퍼센트("+100%" 108px)와 사이 공백을 더해도 카드 안쪽 폭 368px(7타일) 안이다. 더 긴 이름(WEAPON DAMAGE·DASH RECHARGE)은 넘쳐서 뺐고,
        // 넘치면 UpgradeCardPainter가 예외로 알린다.
        public static string Label(PlayerStat stat)
        {
            switch (stat)
            {
                case PlayerStat.Damage: return "GUN DAMAGE";
                case PlayerStat.MoveSpeed: return "MOVE SPEED";
                case PlayerStat.MaxHp: return "MAX HP";
                case PlayerStat.Regen: return "HP REGEN";
                case PlayerStat.PickupRadius: return "PICKUP RANGE";
                case PlayerStat.PickupSpeed: return "PICKUP SPEED";
                case PlayerStat.Recharge: return "RECHARGE";
                case PlayerStat.MaxAmmo: return "MAX AMMO";
                default: throw new System.Exception($"[PickShoot] PlayerStatModifiers.Label: 알 수 없는 스탯 {stat}.");
            }
        }

        // 총계 표기: 배율은 "10%", 가산은 "3". 부호는 음수(언커먼·레어 유물의 패널티)에만 — "+"를 늘 붙이면 값 열이 넓어져 100중첩 전에 넘친다.
        // 0도 "0%"·"0"으로 적는다. 일시정지·디버그 창의 스탯 총계가 쓴다.
        public static string Format(PlayerStat stat, int value)
        {
            return IsPercent(stat) ? $"{value}%" : value.ToString();
        }

        // 수정치 표기: 그 유물이 더하는 값이라 양수에도 부호를 붙인다 ("+10%"·"+3"). 유물의 선택 카드와 보기 카드가 쓴다 (2026-10-07).
        public static string FormatSigned(PlayerStat stat, int value)
        {
            string sign = value >= 0 ? "+" : "";
            return IsPercent(stat) ? $"{sign}{value}%" : sign + value;
        }
    }
}
