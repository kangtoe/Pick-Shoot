using UnityEditor;
using UnityEngine;

namespace PickShoot
{
    // 플레이 모드 디버그 도구 창 (메뉴 PickShoot/Debug Tools). 장기 보존 도구 (DEVELOPMENT §2.1).
    // 씬 참조 없이 플레이 중인 오브젝트를 찾아 조작하므로 씬·프리팹을 건드리지 않고, 빌드에도 들어가지 않는다.
    // 현재 제공: 무기 링 테스트용 권총 추가 / 마지막 무기 버리기 / 자루별 레벨업.
    public class DebugToolsWindow : EditorWindow
    {
        const string PistolAssetPath = "Assets/Data/Weapons/Pistol.asset";

        [MenuItem("PickShoot/Debug Tools")]
        static void Open()
        {
            GetWindow<DebugToolsWindow>("Debug Tools");
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
                    // 자루 단위 레벨업 (Weapons §3). 카드 없이 즉시 올려 탄창 확대·HUD 핍 배치를 확인한다. 레벨 상한은 없다.
                    if (GUILayout.Button("Lv +", GUILayout.Width(48f)))
                        w.LevelUp();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!weapons.HasEmptySlot))
                {
                    if (GUILayout.Button("Add Pistol"))
                        weapons.AddWeapon(LoadPistol());
                }
                // 최소 1자루는 유지 (Weapons §2 시작 슬롯 1). PlayerWeapons.RemoveLastWeapon도 같은 조건을 검사한다.
                using (new EditorGUI.DisabledScope(slots.Count <= 1))
                {
                    if (GUILayout.Button("Remove Last"))
                        weapons.RemoveLastWeapon();
                }
            }
        }

        static WeaponData LoadPistol()
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(PistolAssetPath);
            if (data == null)
                throw new System.Exception($"[PickShoot] DebugToolsWindow: 권총 WeaponData를 찾을 수 없음: {PistolAssetPath}");
            return data;
        }
    }
}
