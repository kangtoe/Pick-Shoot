using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 플레이어 스탯 7개의 총계와 그 출처인 보유 유물 (Relics §2). 플레이어 오브젝트에 붙고, 읽는 쪽(이동·체력·회수·무기)은 같은 오브젝트에서 GetComponent로 찾는다.
    // 스탯의 기본값은 각 읽는 쪽이 제 직렬화 필드로 가지고, 여기는 유물이 더한 수정치의 합만 둔다 — 배율 스탯은 1 + 퍼센트 합 ÷ 100, 최대 체력·재생은 정수 합.
    // 효과의 실제 적용은 읽는 쪽이 한다: 이동속도·대시 쿨다운은 PlayerMovement, 최대 체력·재생은 PlayerHealth, 자석 반경·속도는 PlayerPickup,
    // 공격력 배율은 PlayerWeapons가 발사 때 Weapon에 넘긴다.
    // 유물은 획득 순서대로 쌓이고 같은 유물이 여럿일 수 있다 (커먼 유물의 중첩). 총계는 더할 때마다 처음부터 다시 합산한다 — 빼기가 없어 어긋날 일이 없다.
    // 플레이어와 함께 파괴된다 — 사망 뒤에는 읽는 쪽도 없다. 재시작은 씬 리로드라 0에서 시작한다.
    public class PlayerStats : MonoBehaviour
    {
        readonly List<RelicData> relics = new List<RelicData>();
        PlayerStatModifiers total;

        public IReadOnlyList<RelicData> Relics => relics;
        public PlayerStatModifiers Total => total;
        public float DamageMultiplier => Multiplier(PlayerStat.Damage);
        public float MoveSpeedMultiplier => Multiplier(PlayerStat.MoveSpeed);
        public int MaxHpBonus => total.maxHp;
        public int Regen => total.regen;
        public float PickupRadiusMultiplier => Multiplier(PlayerStat.PickupRadius);
        public float PickupSpeedMultiplier => Multiplier(PlayerStat.PickupSpeed);
        public float RechargeMultiplier => Multiplier(PlayerStat.Recharge);
        // 유물을 얻어 총계가 바뀔 때마다 — 총계가 새 값으로 바뀐 뒤.
        public event System.Action Changed;

        // 같은 유물을 몇 개 가졌나. 카드의 "OWNED n → n+1"이 쓴다.
        public int CountOf(RelicData relic)
        {
            if (relic == null)
                throw new System.Exception("[PickShoot] PlayerStats.CountOf: relic이 null.");
            int count = 0;
            foreach (var r in relics)
                if (r == relic) count++;
            return count;
        }

        public void AddRelic(RelicData relic)
        {
            if (relic == null)
                throw new System.Exception("[PickShoot] PlayerStats.AddRelic: relic이 null.");
            relics.Add(relic);

            var sum = new PlayerStatModifiers();
            foreach (var r in relics)
                sum += r.Modifiers;
            total = sum;

            // 패널티가 겹쳐 배율이 0 이하가 되면 이동·회수·사격이 멎거나 뒤집히므로 얻는 순간 알린다. 최대 체력 하한은 PlayerHealth가 본다.
            foreach (var s in PlayerStatModifiers.Stats)
                if (PlayerStatModifiers.IsPercent(s)) Multiplier(s);
            if (total.regen < 0)
                throw new System.Exception($"[PickShoot] PlayerStats: 재생 총계가 음수 ({total.regen}). '{relic.DisplayName}'까지 더한 유물의 재생 합 확인.");

            Changed?.Invoke();
        }

        float Multiplier(PlayerStat stat)
        {
            int percent = total.Get(stat);
            float m = 1f + percent / 100f;
            if (m <= 0f)
                throw new System.Exception($"[PickShoot] PlayerStats: {stat} 배율이 0 이하 ({percent}%). 유물의 패널티 합이 기본값을 다 깎음.");
            return m;
        }
    }
}
