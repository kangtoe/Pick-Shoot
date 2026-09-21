using UnityEditor;
using UnityEngine;

namespace PickShoot
{
    // 플레이 모드 디버그 도구 창 (메뉴 PickShoot/Debug Tools). 장기 보존 도구 (DEVELOPMENT §2.1).
    // 씬 참조 없이 플레이 중인 오브젝트를 찾아 조작하므로 씬·프리팹을 건드리지 않고, 빌드에도 들어가지 않는다.
    // 현재 제공: 무기 종류를 골라 추가 / 마지막 무기 버리기 / 자루별 레벨업.
    public class DebugToolsWindow : EditorWindow
    {
        const int AddButtonsPerRow = 3;

        // 프로젝트의 모든 WeaponData 자산. 획득 카드 풀(UpgradeManager.weaponPool)에 아직 없는 무기도 시험할 수 있게
        // 씬 참조가 아니라 자산 DB에서 직접 찾는다. 자산이 추가·삭제되면 OnProjectChange에서 다시 찾는다.
        WeaponData[] weaponAssets = new WeaponData[0];

        [MenuItem("PickShoot/Debug Tools")]
        static void Open()
        {
            GetWindow<DebugToolsWindow>("Debug Tools");
        }

        void OnEnable()
        {
            RefreshWeaponAssets();
        }

        void OnProjectChange()
        {
            RefreshWeaponAssets();
        }

        void RefreshWeaponAssets()
        {
            var guids = AssetDatabase.FindAssets("t:WeaponData");
            weaponAssets = new WeaponData[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                weaponAssets[i] = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (weaponAssets[i] == null)
                    throw new System.Exception($"[PickShoot] DebugToolsWindow: WeaponData 자산 로드 실패: {path}");
            }
        }

        // 플레이 중 슬롯 상태(탄약·레벨)가 바뀌는 대로 창을 갱신한다 (약 10Hz).
        void OnInspectorUpdate()
        {
            Repaint();
        }

        void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 동작합니다.", MessageType.Info);
                return;
            }
            var weapons = FindFirstObjectByType<PlayerWeapons>();
            if (weapons == null)
            {
                EditorGUILayout.HelpBox("씬에 PlayerWeapons가 없습니다 (플레이어 사망 후에는 파괴됨).", MessageType.Warning);
                return;
            }

            DrawWeapons(weapons);
        }

        void DrawWeapons(PlayerWeapons weapons)
        {
            var slots = weapons.Slots;
            GUILayout.Label($"Weapons  {slots.Count}/{PlayerWeapons.MaxSlots}", EditorStyles.boldLabel);
            for (int i = 0; i < slots.Count; i++)
            {
                var w = slots[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}. {w.Data.DisplayName}   Lv {w.Level}   {w.CurrentAmmo}/{w.MaxAmmo}");
                    // 자루 단위 레벨업 (Weapons §3). 카드 없이 즉시 올려 탄창 확대·HUD 핍 배치를 확인한다. 최대 레벨(Weapon.MaxLevel)이면 잠근다.
                    using (new EditorGUI.DisabledScope(w.IsMaxLevel))
                    {
                        if (GUILayout.Button("Lv +", GUILayout.Width(48f)))
                            w.LevelUp();
                    }
                }
            }

            DrawAddButtons(weapons);

            // 최소 1자루는 유지 (Weapons §2 시작 슬롯 1). PlayerWeapons.RemoveLastWeapon도 같은 조건을 검사한다.
            using (new EditorGUI.DisabledScope(slots.Count <= 1))
            {
                if (GUILayout.Button("Remove Last"))
                    weapons.RemoveLastWeapon();
            }
        }

        // 무기 종류마다 추가 버튼 하나 — 한 번 클릭으로 그 종류의 새 자루가 다음 슬롯에 들어간다 (Weapons §3, 항상 Lv.1).
        // 종류가 늘어도 창이 옆으로 넓어지지 않게 한 줄에 AddButtonsPerRow개씩 끊는다.
        void DrawAddButtons(PlayerWeapons weapons)
        {
            GUILayout.Label("Add Weapon", EditorStyles.boldLabel);
            if (weaponAssets.Length == 0)
            {
                EditorGUILayout.HelpBox("프로젝트에 WeaponData 자산이 없습니다 (검색: t:WeaponData).", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(!weapons.HasEmptySlot))
            {
                for (int rowStart = 0; rowStart < weaponAssets.Length; rowStart += AddButtonsPerRow)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        int rowEnd = Mathf.Min(rowStart + AddButtonsPerRow, weaponAssets.Length);
                        for (int i = rowStart; i < rowEnd; i++)
                        {
                            if (GUILayout.Button(weaponAssets[i].DisplayName))
                                weapons.AddWeapon(weaponAssets[i]);
                        }
                    }
                }
            }
        }
    }
}
