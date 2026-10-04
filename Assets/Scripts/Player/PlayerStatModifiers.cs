using UnityEngine;

namespace PickShoot
{
    // 유물이 올리는 플레이어 스탯 7개 (Relics §2). 수정치 묶음(PlayerStatModifiers)의 칸과 카드의 줄 순서가 이 순서다.
    public enum PlayerStat
    {
        Damage,         // 공격력 배율 (%) — 모든 무기의 피해에 곱한다
        MoveSpeed,      // 이동속도 (%)
        MaxHp,          // 최대 체력 (+정수)
        Regen,          // 재생 (+정수) — 주기마다 체력 1 회복, 수치가 클수록 주기가 짧다 (PlayerHealth)
        PickupRadius,   // 탄환 회수 범위 — 자석 반경 (%)
        PickupSpeed,    // 탄환 회수 속도 — 자석이 끌어오는 속도 (%)
        Recharge,       // 능력 리차지 속도 — 대시 등 모든 능력의 쿨다운에 공통 (%)
    }

    // 플레이어 스탯에 더하는 수정치 묶음. 유물 하나가 묶음 하나를 가지고, PlayerStats가 보유 유물의 묶음을 전부 더해 총계를 만든다.
    // 배율 스탯은 퍼센트 정수(10 = +10%), 최대 체력·재생은 그대로 더하는 정수 — 카드 글꼴이 숫자와 %뿐이라 소수를 두지 않는다.
    // 커먼 유물은 한 칸만 채우지만, 유니크 유물(Relics §3)의 "공격력↑ 이동속도↓" 같은 복합·패널티 효과도 같은 묶음으로 적는다 — 음수 허용.
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
            };
        }

        // 배율 스탯(퍼센트 표기)인가, 정수 가산인가.
        public static bool IsPercent(PlayerStat stat)
        {
            return stat != PlayerStat.MaxHp && stat != PlayerStat.Regen;
        }

        // 카드 줄의 항목 이름. 시트 글꼴(A-Z)로 짧게 — 배율 스탯은 5글자(54텍셀) 이하여야 값 열("990%" → 화살표 → "1000%")과 함께 카드 안쪽 폭(7타일)에
        // 들어간다 (UpgradeCardPainter가 넘치면 예외). 그래서 회수 범위는 RANGE, 능력 리차지는 SKILL — RECHARGE·MAGNET은 중첩이 쌓이면 넘친다.
        public static string Label(PlayerStat stat)
        {
            switch (stat)
            {
                case PlayerStat.Damage: return "DMG";
                case PlayerStat.MoveSpeed: return "SPEED";
                case PlayerStat.MaxHp: return "MAX HP";
                case PlayerStat.Regen: return "REGEN";
                case PlayerStat.PickupRadius: return "RANGE";
                case PlayerStat.PickupSpeed: return "PULL";
                case PlayerStat.Recharge: return "SKILL";
                default: throw new System.Exception($"[PickShoot] PlayerStatModifiers.Label: 알 수 없는 스탯 {stat}.");
            }
        }

        // 카드 값 표기: 배율은 "10%", 가산은 "3". 부호는 음수(유니크 유물의 패널티)에만 — "+"를 늘 붙이면 값 열이 넓어져 100중첩 전에 카드를 넘친다.
        // 0도 "0%"·"0"으로 적는다 — 아직 없는 스탯의 "지금" 열.
        public static string Format(PlayerStat stat, int value)
        {
            return IsPercent(stat) ? $"{value}%" : value.ToString();
        }
    }
}
