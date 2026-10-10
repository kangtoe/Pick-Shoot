using UnityEngine;

namespace PickShoot
{
    public enum UpgradeKind
    {
        AcquireWeapon,
        LevelUp,
        Mod,
        Relic,
    }

    // 업그레이드 선택 카드 하나. Kind에 따라 필드가 달라진다:
    //   AcquireWeapon → acquireData 사용. Levels는 시작 레벨
    //   LevelUp       → targetSlotIndex 사용 (자루 단위 레벨업, GDD §7.3). Levels는 오르는 단계 수
    //   Mod           → targetSlotIndex + modData 사용 (그 자루에 개조를 붙인다, Mods §3). Levels는 쓰지 않는다 (0)
    //   Relic         → relicData 사용 (유물 획득, Relics §2 — 커먼 유물은 이미 가진 것도 다시 얻어 중첩한다). Levels는 쓰지 않는다 (0)
    // Levels는 지금 모든 무기 카드가 1이다 — 한 장이 레벨을 둘 이상 올리는 보너스 카드는 아직 없고, 생기면 카드 제목에 `(+n)`이 붙는다 (UpgradeCardPainter, 2026-10-08).
    // 같은 종류·같은 대상·같은 레벨 수면 같은 카드다 — 리롤이 뺄 카드를 후보에서 찾는 기준.
    public readonly struct UpgradeOption : System.IEquatable<UpgradeOption>
    {
        public readonly UpgradeKind Kind;
        public readonly WeaponData AcquireData;   // AcquireWeapon 전용
        public readonly int TargetSlotIndex;      // LevelUp·Mod 전용
        public readonly ModData ModData;          // Mod 전용
        public readonly RelicData RelicData;      // Relic 전용
        public readonly int Levels;               // 획득·레벨업 카드 전용 — 이 카드가 올리는 레벨 수 (1 이상)

        UpgradeOption(UpgradeKind kind, WeaponData acquireData, int targetSlotIndex, ModData modData, RelicData relicData, int levels)
        {
            Kind = kind;
            AcquireData = acquireData;
            TargetSlotIndex = targetSlotIndex;
            ModData = modData;
            RelicData = relicData;
            Levels = levels;
        }

        // levels: 획득 뒤의 시작 레벨. 1이 보통의 획득 카드.
        public static UpgradeOption Acquire(WeaponData data, int levels = 1)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] UpgradeOption.Acquire: data가 null.");
            RequireLevels(levels, nameof(Acquire));
            return new UpgradeOption(UpgradeKind.AcquireWeapon, data, -1, null, null, levels);
        }

        // levels: 올리는 단계 수. 1이 보통의 레벨업 카드. 대상 자루의 레벨 + levels가 최대 레벨을 넘는지는 자루를 아는 쪽(카드 그리기·Apply)이 검사한다.
        public static UpgradeOption LevelUpSlot(int slotIndex, int levels = 1)
        {
            if (slotIndex < 0)
                throw new System.Exception($"[PickShoot] UpgradeOption.LevelUpSlot: slotIndex 음수 ({slotIndex}).");
            RequireLevels(levels, nameof(LevelUpSlot));
            return new UpgradeOption(UpgradeKind.LevelUp, null, slotIndex, null, null, levels);
        }

        // 개조가 없는 자루에만 낸다 — 이미 붙었는지는 자루를 아는 쪽(후보 만들기·카드 그리기·Apply)이 검사한다.
        public static UpgradeOption ModSlot(int slotIndex, ModData mod)
        {
            if (slotIndex < 0)
                throw new System.Exception($"[PickShoot] UpgradeOption.ModSlot: slotIndex 음수 ({slotIndex}).");
            if (mod == null)
                throw new System.Exception("[PickShoot] UpgradeOption.ModSlot: mod가 null.");
            return new UpgradeOption(UpgradeKind.Mod, null, slotIndex, mod, null, 0);
        }

        public static UpgradeOption Relic(RelicData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] UpgradeOption.Relic: data가 null.");
            return new UpgradeOption(UpgradeKind.Relic, null, -1, null, data, 0);
        }

        static void RequireLevels(int levels, string factory)
        {
            if (levels < 1 || levels > Weapon.MaxLevel)
                throw new System.Exception($"[PickShoot] UpgradeOption.{factory}: levels {levels}는 1~{Weapon.MaxLevel}이어야 함.");
        }

        public bool Equals(UpgradeOption other)
        {
            return Kind == other.Kind && AcquireData == other.AcquireData && TargetSlotIndex == other.TargetSlotIndex && ModData == other.ModData && RelicData == other.RelicData && Levels == other.Levels;
        }

        public override bool Equals(object obj)
        {
            return obj is UpgradeOption other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Kind, AcquireData, TargetSlotIndex, ModData, RelicData, Levels);
        }

        public void Apply(PlayerWeapons weapons, PlayerStats stats)
        {
            switch (Kind)
            {
                case UpgradeKind.AcquireWeapon:
                    weapons.AddWeapon(AcquireData);
                    // 새 자루는 Lv.1로 들어오므로(PlayerWeapons.AddSlot) 시작 레벨까지 올린다. 마지막 슬롯이 방금 더한 자루다.
                    for (int i = 1; i < Levels; i++)
                        weapons.Slots[weapons.Slots.Count - 1].LevelUp();
                    break;

                case UpgradeKind.LevelUp:
                {
                    if (TargetSlotIndex < 0 || TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeOption.Apply(LevelUp): slotIndex {TargetSlotIndex} 범위 밖 (현재 슬롯 {weapons.Slots.Count}개). 선택 후 슬롯이 변경되었을 가능성.");
                    var w = weapons.Slots[TargetSlotIndex];
                    if (w.Level + Levels > Weapon.MaxLevel)
                        throw new System.Exception($"[PickShoot] UpgradeOption.Apply(LevelUp): '{w.Data.DisplayName}' 레벨 {w.Level}에 {Levels}를 더하면 최대 {Weapon.MaxLevel}을 넘음. 후보를 만들 때 걸러야 함 (UpgradeManager).");
                    for (int i = 0; i < Levels; i++)
                        w.LevelUp();
                    break;
                }

                case UpgradeKind.Mod:
                    if (TargetSlotIndex < 0 || TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeOption.Apply(Mod): slotIndex {TargetSlotIndex} 범위 밖 (현재 슬롯 {weapons.Slots.Count}개). 선택 후 슬롯이 변경되었을 가능성.");
                    weapons.Slots[TargetSlotIndex].AttachMod(ModData);
                    break;

                case UpgradeKind.Relic:
                    stats.AddRelic(RelicData);
                    break;

                default:
                    throw new System.Exception($"[PickShoot] UpgradeOption.Apply: 알 수 없는 Kind {Kind}");
            }
        }
    }
}
