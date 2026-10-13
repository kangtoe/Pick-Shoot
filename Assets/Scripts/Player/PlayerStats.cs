using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 플레이어 스탯 8개의 총계와 그 출처 — 러닝 시작에 고른 캐릭터(Characters §1)와 보유 유물(Relics §2). 플레이어 오브젝트에 붙고, 읽는 쪽(이동·체력·회수·무기)은
    // 같은 오브젝트에서 GetComponent로 찾는다.
    // 스탯의 기본값은 각 읽는 쪽이 제 직렬화 필드로 가지고, 여기는 캐릭터·유물이 더한 수정치의 합만 둔다 — 배율 스탯은 1 + 퍼센트 합 ÷ 100, 최대 체력·재생·최대 탄약은 정수 합.
    // 효과의 실제 적용은 읽는 쪽이 한다: 이동속도·능력 쿨다운은 PlayerMovement, 최대 체력·재생은 PlayerHealth, 자석 반경·속도는 PlayerPickup,
    // 공격력 배율은 PlayerWeapons가 발사 때 Weapon에 넘기고 최대 탄약 가산은 PlayerWeapons가 자루마다 넣는다.
    // 총계 = 캐릭터 수정치 + 유물 수정치 합 (Characters §5). 유물은 획득 순서대로 쌓이고 같은 유물이 여럿일 수 있다 (커먼 유물의 중첩). 총계는 바뀔 때마다 처음부터
    // 다시 합산한다 — 빼기가 없어 어긋날 일이 없고, 시작 화면에서 ESC로 돌아가 캐릭터를 다시 고르면(SetCharacter) 그대로 바뀐다.
    // 플레이어와 함께 파괴된다 — 사망 뒤에는 읽는 쪽도 없다. 재시작은 씬 리로드라 캐릭터 없음·유물 0에서 시작한다.
    public class PlayerStats : MonoBehaviour
    {
        readonly List<RelicData> relics = new List<RelicData>();
        // 러닝 시작에 고른 캐릭터. 고르기 전에는 null — 총계는 유물 합뿐이다.
        CharacterData character;
        PlayerStatModifiers total;

        public CharacterData Character => character;
        public IReadOnlyList<RelicData> Relics => relics;
        public PlayerStatModifiers Total => total;
        public float DamageMultiplier => Multiplier(PlayerStat.Damage);
        public float MoveSpeedMultiplier => Multiplier(PlayerStat.MoveSpeed);
        public int MaxHpBonus => total.maxHp;
        public int Regen => total.regen;
        public float PickupRadiusMultiplier => Multiplier(PlayerStat.PickupRadius);
        public float PickupSpeedMultiplier => Multiplier(PlayerStat.PickupSpeed);
        public float RechargeMultiplier => Multiplier(PlayerStat.Recharge);
        // 모든 자루의 최대 탄약에 더하는 정수 (Characters §3·§5). Weapon이 탄창 계산에 넣는다.
        public int MaxAmmoBonus => total.maxAmmo;
        // 캐릭터를 고르거나 유물을 얻어 총계가 바뀔 때마다 — 총계가 새 값으로 바뀐 뒤.
        public event System.Action Changed;

        // 같은 유물을 몇 개 가졌나. 카드·타일의 보유 수 배지(`X n`)가 쓴다.
        public int CountOf(RelicData relic)
        {
            if (relic == null)
                throw new System.Exception("[PickShoot] PlayerStats.CountOf: relic이 null.");
            int count = 0;
            foreach (var r in relics)
                if (r == relic) count++;
            return count;
        }

        // 러닝 시작 화면이 고른 캐릭터를 넣는다 (UpgradeManager). 다시 부르면(ESC로 돌아가 다른 캐릭터) 전의 캐릭터 몫은 총계에서 빠진다.
        public void SetCharacter(CharacterData newCharacter)
        {
            if (newCharacter == null)
                throw new System.Exception("[PickShoot] PlayerStats.SetCharacter: character가 null.");
            character = newCharacter;
            Recompute($"캐릭터 '{newCharacter.DisplayName}'");
            Changed?.Invoke();
        }

        public void AddRelic(RelicData relic)
        {
            if (relic == null)
                throw new System.Exception("[PickShoot] PlayerStats.AddRelic: relic이 null.");
            relics.Add(relic);
            Recompute($"'{relic.DisplayName}'까지 더한 유물");
            Changed?.Invoke();
        }

        // 총계를 처음부터 다시 합산한다. cause는 예외 문구용 — 이번에 더한 것.
        void Recompute(string cause)
        {
            var sum = character != null ? character.Modifiers : new PlayerStatModifiers();
            foreach (var r in relics)
                sum += r.Modifiers;
            total = sum;

            // 패널티가 겹쳐 배율이 0 이하가 되면 이동·회수·사격이 멎거나 뒤집히므로 더하는 순간 알린다. 최대 체력 하한은 PlayerHealth가, 탄창 하한은 Weapon이 본다.
            foreach (var s in PlayerStatModifiers.Stats)
                if (PlayerStatModifiers.IsPercent(s)) Multiplier(s);
            if (total.regen < 0)
                throw new System.Exception($"[PickShoot] PlayerStats: 재생 총계가 음수 ({total.regen}). {cause}의 재생 합 확인.");
        }

        float Multiplier(PlayerStat stat)
        {
            int percent = total.Get(stat);
            float m = 1f + percent / 100f;
            if (m <= 0f)
                throw new System.Exception($"[PickShoot] PlayerStats: {stat} 배율이 0 이하 ({percent}%). 캐릭터·유물의 패널티 합이 기본값을 다 깎음.");
            return m;
        }
    }
}
