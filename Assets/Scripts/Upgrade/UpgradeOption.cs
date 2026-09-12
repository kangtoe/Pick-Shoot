using UnityEngine;

namespace PickShoot
{
    public enum UpgradeKind
    {
        AcquireWeapon,
        LevelUp,
    }

    // 업그레이드 선택 카드 하나. Kind에 따라 필드가 달라진다:
    //   AcquireWeapon → acquireData 사용
    //   LevelUp       → targetSlotIndex 사용 (자루 단위 레벨업, GDD §7.3)
    public readonly struct UpgradeOption
    {
        public readonly UpgradeKind Kind;
        public readonly WeaponData AcquireData;   // AcquireWeapon 전용
        public readonly int TargetSlotIndex;      // LevelUp 전용

        UpgradeOption(UpgradeKind kind, WeaponData acquireData, int targetSlotIndex)
        {
            Kind = kind;
            AcquireData = acquireData;
            TargetSlotIndex = targetSlotIndex;
        }

        public static UpgradeOption Acquire(WeaponData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] UpgradeOption.Acquire: data가 null.");
            return new UpgradeOption(UpgradeKind.AcquireWeapon, data, -1);
        }

        public static UpgradeOption LevelUpSlot(int slotIndex)
        {
            if (slotIndex < 0)
                throw new System.Exception($"[PickShoot] UpgradeOption.LevelUpSlot: slotIndex 음수 ({slotIndex}).");
            return new UpgradeOption(UpgradeKind.LevelUp, null, slotIndex);
        }

        public string BuildLabel(PlayerWeapons weapons)
        {
            switch (Kind)
            {
                case UpgradeKind.AcquireWeapon:
                    return $"새 [{AcquireData.DisplayName}] 획득 (Lv.1)";

                case UpgradeKind.LevelUp:
                    var w = weapons.Slots[TargetSlotIndex];
                    return $"[슬롯 {TargetSlotIndex + 1}] {w.Data.DisplayName}  Lv.{w.Level} → Lv.{w.Level + 1}";

                default:
                    throw new System.Exception($"[PickShoot] UpgradeOption.BuildLabel: 알 수 없는 Kind {Kind}");
            }
        }

        public void Apply(PlayerWeapons weapons)
        {
            switch (Kind)
            {
                case UpgradeKind.AcquireWeapon:
                    weapons.AddWeapon(AcquireData);
                    break;

                case UpgradeKind.LevelUp:
                    if (TargetSlotIndex < 0 || TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeOption.Apply(LevelUp): slotIndex {TargetSlotIndex} 범위 밖 (현재 슬롯 {weapons.Slots.Count}개). 선택 후 슬롯이 변경되었을 가능성.");
                    weapons.Slots[TargetSlotIndex].LevelUp();
                    break;

                default:
                    throw new System.Exception($"[PickShoot] UpgradeOption.Apply: 알 수 없는 Kind {Kind}");
            }
        }
    }
}
