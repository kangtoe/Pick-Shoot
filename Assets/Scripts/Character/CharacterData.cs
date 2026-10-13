using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 스페이스 능력의 종류 (Characters §2). 대시는 PlayerMovement가 맡고, 회수 펄스·충격파는 구현 2단계에서 얹는다 — 그때까지 자산이 그 둘을 가리키면
    // PlayerMovement.SetAbility가 예외로 알린다 (1단계는 3종 모두 대시, Characters §5).
    public enum AbilityKind { Dash, Pulse, Shockwave }

    // 캐릭터 정의 (Characters §1·§3). 자산은 Assets/Data/Characters — 수치의 SSOT. 러닝 시작 화면(UpgradeManager.PresentStartAndWait)의 캐릭터 단계가 후보로 쓰고,
    // 고르면 플레이어 껍데기에 그림(SpriteWalkCycle 프레임)·스탯 수정치(PlayerStats)·능력(PlayerMovement)을 채운다. 캐릭터가 바꾸는 것은 이 셋과 시작 무기
    // 후보뿐이다 — 캐릭터별 규칙(특성)은 두지 않는다.
    [CreateAssetMenu(menuName = "PickShoot/Character Data", fileName = "CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [SerializeField] string displayName = "Character";
        // 걷기 주기 프레임 (platformer_characters.png의 헬멧 캐릭터, 32×32 격자 — 정지·걷기 2장). 0번이 정지 포즈이고 시작 화면의 타일·카드 전시 칸이 쓴다.
        // SpriteWalkCycle이 이동 거리로 돌린다 — 장 수에 제한은 없다.
        [SerializeField] Sprite[] frames = new Sprite[0];
        // 러닝 내내 유물 총계에 더해지는 수정치 묶음 — 유물과 같은 척도 (Relics §2). 패널티는 두지 않으므로 음수면 Validate가 예외 (Characters §1).
        [SerializeField] PlayerStatModifiers modifiers;
        // 시작 화면 무기 단계의 후보 — 획득 풀(UpgradeManager.weaponPool)의 부분집합. 획득 카드 풀은 바꾸지 않는다 (Characters §1).
        [SerializeField] List<WeaponData> startWeapons = new List<WeaponData>();
        [SerializeField] AbilityKind abilityKind = AbilityKind.Dash;
        // 능력을 쓴 순간부터 다음 사용까지 (초). 리차지 스탯이 나눈다 (PlayerMovement).
        [SerializeField, Min(0f)] float abilityCooldown = 1.5f;
        // 캐릭터 카드의 능력 줄 아래 한 줄 설명 (`PULLS NEARBY BULLETS`). 시트 글꼴 문자(영문 대문자·숫자·% + - ! ( ) / .)만, 카드 안쪽 폭에 한 줄로 들어야 한다 —
        // 넘치면 UpgradeCardPainter가 예외로 알린다.
        [SerializeField] string abilityDescription = "";

        public string DisplayName => displayName;
        public Sprite[] Frames => frames;
        public Sprite IdleSprite => frames[0];
        public PlayerStatModifiers Modifiers => modifiers;
        public IReadOnlyList<WeaponData> StartWeapons => startWeapons;
        public AbilityKind AbilityKind => abilityKind;
        public float AbilityCooldown => abilityCooldown;
        public string AbilityDescription => abilityDescription;

        // 카드의 능력 줄에 적는 이름 (`ABILITY DASH`). 시트 글꼴 문자만.
        public static string AbilityUiName(AbilityKind kind)
        {
            switch (kind)
            {
                case AbilityKind.Dash: return "DASH";
                case AbilityKind.Pulse: return "PULSE";
                case AbilityKind.Shockwave: return "SHOCKWAVE";
                default: throw new System.Exception($"[PickShoot] CharacterData.AbilityUiName: 알 수 없는 능력 {kind}.");
            }
        }

        // 누락·모순을 자산 이름까지 지목해 예외로 알린다. 후보로 쓰는 쪽(UpgradeManager)이 Awake에서 획득 풀과 함께 부른다.
        public void Validate(IReadOnlyList<WeaponData> weaponPool)
        {
            if (frames == null || frames.Length < 2)
                throw new System.Exception($"[PickShoot] CharacterData '{name}'.frames는 2장 이상이어야 함 (정지 포즈 + 걷기).");
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null)
                    throw new System.Exception($"[PickShoot] CharacterData '{name}'.frames[{i}] 미설정.");
            if (startWeapons.Count == 0)
                throw new System.Exception($"[PickShoot] CharacterData '{name}'.startWeapons가 비어 있음. 시작 화면 무기 단계의 후보가 최소 1종 필요.");
            for (int i = 0; i < startWeapons.Count; i++)
            {
                if (startWeapons[i] == null)
                    throw new System.Exception($"[PickShoot] CharacterData '{name}'.startWeapons[{i}]가 null.");
                if (!Contains(weaponPool, startWeapons[i]))
                    throw new System.Exception($"[PickShoot] CharacterData '{name}'.startWeapons[{i}] '{startWeapons[i].DisplayName}'가 획득 풀(UpgradeManager.weaponPool)에 없음. 시작 무기 후보는 획득 풀의 부분집합 (Characters §1).");
            }
            if (modifiers.IsZero)
                throw new System.Exception($"[PickShoot] CharacterData '{name}'의 수정치가 전부 0 — 기준점 캐릭터는 두지 않는다 (Characters §1).");
            foreach (var s in PlayerStatModifiers.Stats)
                if (modifiers.Get(s) < 0)
                    throw new System.Exception($"[PickShoot] CharacterData '{name}'의 {s}가 음수 ({modifiers.Get(s)}). 캐릭터에 패널티는 두지 않는다 (Characters §1).");
            if (abilityCooldown <= 0f)
                throw new System.Exception($"[PickShoot] CharacterData '{name}'.abilityCooldown이 0 이하 ({abilityCooldown}).");
            if (string.IsNullOrEmpty(abilityDescription))
                throw new System.Exception($"[PickShoot] CharacterData '{name}'.abilityDescription이 비어 있음. 카드의 능력 줄 아래 한 줄 설명 필요 (Characters §4).");
        }

        static bool Contains(IReadOnlyList<WeaponData> pool, WeaponData weapon)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] == weapon) return true;
            return false;
        }
    }
}
