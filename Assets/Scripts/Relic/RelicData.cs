using UnityEngine;

namespace PickShoot
{
    // 유물 등급 (Relics §1). 지금 정의된 것은 커먼뿐 — 레어·유니크는 로드맵 3·4단계(Relics §5)에서 채운다. 카드 배지 문구는 이름의 영문 대문자.
    public enum RelicGrade { Common, Rare, Unique }

    // 유물 정의 (Relics §2). 자산은 Assets/Data/Relics — 수치의 SSOT. 보상 화면(UpgradeManager.relicPool)이 후보로 쓰고, 획득하면 PlayerStats에 쌓인다.
    // 커먼 유물은 반복 획득·중첩이 가능하고 스탯 수정치 묶음 하나로 효과가 끝난다 — 새 플레이 규칙은 두지 않는다.
    [CreateAssetMenu(menuName = "PickShoot/Relic Data", fileName = "RelicData")]
    public class RelicData : ScriptableObject
    {
        [SerializeField] string displayName = "Relic";
        [SerializeField] RelicGrade grade = RelicGrade.Common;
        // 카드 전시 칸의 그림. 지금은 임시 조각 — Tiles 시트의 열쇠를 유물별 색으로 바꾼 사본(Assets/Art/UI/relic_keys.png). 유물별 그림은 고유 아트 때 (Relics §2).
        [SerializeField] Sprite icon;
        // 타일 툴팁(TooltipPainter)의 설명 문단. 시트 글꼴 문자(영문 대문자·숫자·% + - ! ( ) / .)만, 단어 단위로 접힌다 — 한 단어가 툴팁 폭을 넘치면 그릴 때 예외. 비어 있으면 UpgradeManager가 시작 때 예외로 알린다.
        [SerializeField] string description = "";
        [SerializeField] PlayerStatModifiers modifiers;

        public string DisplayName => displayName;
        public RelicGrade Grade => grade;
        public Sprite Icon => icon;
        public string Description => description;
        public PlayerStatModifiers Modifiers => modifiers;
    }
}
