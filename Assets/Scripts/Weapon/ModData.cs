using UnityEngine;

namespace PickShoot
{
    // 무기 개조 정의 (Mods §1·§2). 자산은 Assets/Data/Mods — 개조의 종류·수치·문구·효과음의 SSOT. 보상 화면(UpgradeManager.modPool)이 후보로 쓰고,
    // 고르면 자루(Weapon.AttachMod)에 붙는다. WeaponData는 건드리지 않는다 — 개조된 자루는 WeaponData에 이 자산을 얹어 읽는다 (Weapon의 ...With 함수).
    // 개조 하나가 쓰는 묶음만 채우고 나머지는 0으로 둔다 — 수치를 바꾸는 것(튕김·관통·폭발 반경)과 새 동작(유도). 0이면 그 효과가 없다.
    // 효과는 날아가는 탄환(튕김 페이즈)에만 작용하고, 발사 순간의 개조로 정해진다 (Bullet.Launch).
    [CreateAssetMenu(menuName = "PickShoot/Mod Data", fileName = "ModData")]
    public class ModData : ScriptableObject
    {
        // UI 이름은 영문 대문자 (`HOMING`). 첫 글자가 빌드 타일 배지의 머리글자(`LV5H`)라 개조끼리 첫 글자가 겹치면 안 된다 (UpgradeManager가 검사).
        [SerializeField] string displayName = "Mod";
        // 개조 카드·보기 카드 본문의 설명 문단 (선택) — 카드의 스탯 줄·특성 줄이 못 하는 말만. 시트 글꼴 문자만, 단어 단위로 접힌다.
        [SerializeField] string description = "";

        [Header("Stat Adds")]
        // 자산의 튕김 횟수·관통에 더한다 (도탄 +3·천공 +2, Mods §2.3·§2.4). 카드에는 합친 값으로 보인다.
        [SerializeField, Min(0)] int bounceAdd = 0;
        [SerializeField, Min(0)] int penetrationAdd = 0;

        [Header("Explosion")]
        // 0이면 없음. 양수면 폭발 탄환이 아닌 무기의 탄환을 이 반경의 폭발 탄환으로 바꾼다 — 그 뒤는 런처와 같은 폭발 탄환 규칙이다
        // (모든 접촉에서 폭발, 반경 안 모든 적에게 무기 피해·넉백, 직격 피해 없음 — Weapons §7, Mods §2.1). 균형은 피해 비율이 아니라 이 반경으로 맞춘다.
        [SerializeField, Min(0f)] float explosionRadius = 0f;
        // 이미 폭발 탄환인 무기(런처)의 폭발 반경 배율. 1이면 바꾸지 않는다.
        [SerializeField, Min(1f)] float explosionRadiusMultiplier = 1f;
        // 이 개조로 폭발 탄환이 된 무기의 폭발음 — 그런 무기의 자산에는 폭발음이 없다. 런처는 자기(WeaponData) 폭발음 그대로다.
        [SerializeField] AudioClip explosionClip;
        // 탄환마다 터져 자주 울리므로 다른 효과음보다 낮춰 둔다 (탄환 튕김음과 같은 이유).
        [SerializeField, Range(0f, 1f)] float explosionVolume = 0.6f;

        [Header("Homing")]
        // 0이면 유도 없음. 양수면 진행 방향의 부채꼴(좌우 homingHalfAngleDegrees, 거리 homingRange) 안 가장 가까운 적을 향해 초당 이 각도까지 진행
        // 방향을 튼다. 속도는 그대로다. 발사 후 homingDelaySeconds 동안은 곧게 난다 (Mods §2.2).
        [SerializeField, Min(0f)] float homingTurnRateDegrees = 0f;
        [SerializeField, Range(0f, 180f)] float homingHalfAngleDegrees = 45f;
        [SerializeField, Min(0f)] float homingRange = 5f;
        [SerializeField, Min(0f)] float homingDelaySeconds = 0.1f;

        public string DisplayName => displayName;
        public string Description => description;
        public int BounceAdd => bounceAdd;
        public int PenetrationAdd => penetrationAdd;
        public float ExplosionRadius => explosionRadius;
        public float ExplosionRadiusMultiplier => explosionRadiusMultiplier;
        public AudioClip ExplosionClip => explosionClip;
        public float ExplosionVolume => explosionVolume;
        public float HomingTurnRateDegrees => homingTurnRateDegrees;
        public float HomingHalfAngleDegrees => homingHalfAngleDegrees;
        public float HomingRange => homingRange;
        public float HomingDelaySeconds => homingDelaySeconds;
        public bool IsHoming => homingTurnRateDegrees > 0f;

        // UI 이름 (`HOMING`)과 빌드 타일 배지의 머리글자 (`H`).
        public string UiName => displayName.ToUpperInvariant();
        public string BadgeMark => UiName.Substring(0, 1);

        // 이 무기의 탄환을 폭발 탄환으로 바꾸는가 — 이미 폭발 탄환인 무기는 반경만 커진다 (Mods §2.1).
        public bool MakesExplosive(WeaponData weapon)
        {
            return explosionRadius > 0f && weapon.ExplosionRadius <= 0f;
        }

        // 효과 없는 자산·배지 머리글자가 없는 이름을 이름까지 지목해 알린다. 쓰는 쪽(UpgradeManager·디버그 도구)이 부른다.
        public void Validate()
        {
            if (string.IsNullOrEmpty(displayName))
                throw new System.Exception($"[PickShoot] ModData '{name}': displayName이 비어 있음. 첫 글자가 타일 배지의 머리글자다.");
            if (bounceAdd == 0 && penetrationAdd == 0 && explosionRadius <= 0f && explosionRadiusMultiplier <= 1f && !IsHoming)
                throw new System.Exception($"[PickShoot] ModData '{name}': 효과가 전부 0 — 효과 없는 개조.");
            if (IsHoming && (homingRange <= 0f || homingHalfAngleDegrees <= 0f))
                throw new System.Exception($"[PickShoot] ModData '{name}': 유도인데 거리 {homingRange}·반각 {homingHalfAngleDegrees}가 0 — 노릴 적이 없다.");
        }
    }
}
