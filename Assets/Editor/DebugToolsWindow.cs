using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PickShoot
{
    // 플레이 모드 디버그 도구 창 (메뉴 PickShoot/Debug Tools). 장기 보존 도구 (DEVELOPMENT §2.1).
    // 씬 참조 없이 플레이 중인 오브젝트를 찾아 조작하므로 씬·프리팹을 건드리지 않고, 빌드에도 들어가지 않는다.
    // 현재 제공: 진행 중인 웨이브 즉시 클리어 / 적 종류를 골라 즉시 스폰 / 무기 종류를 골라 추가 / 마지막 무기 버리기 / 자루별 레벨업 / 경험치 더하기(레벨 업·리롤 적립 시험용).
    public class DebugToolsWindow : EditorWindow
    {
        // 드롭다운 옆 실행 버튼(Spawn/Add) 너비. 드롭다운이 남는 폭을 다 차지한다.
        const float ActionButtonWidth = 64f;
        const float SeparatorMargin = 6f;
        static readonly Color SeparatorColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        // 프로젝트의 모든 WeaponData 자산. 획득 카드 풀(UpgradeManager.weaponPool)에 아직 없는 무기도 시험할 수 있게
        // 씬 참조가 아니라 자산 DB에서 직접 찾는다. 자산이 추가·삭제되면 OnProjectChange에서 다시 찾는다.
        WeaponData[] weaponAssets = new WeaponData[0];
        string[] weaponNames = new string[0];
        int selectedWeapon;
        // 프로젝트의 모든 적 프리팹(EnemyHealth가 붙은 프리팹). 웨이브 편성에 없거나 아직 해금되지 않은 적도 시험할 수 있게
        // 씬의 편성이 아니라 자산 DB에서 직접 찾는다.
        GameObject[] enemyPrefabs = new GameObject[0];
        string[] enemyNames = new string[0];
        int selectedEnemy;

        [MenuItem("PickShoot/Debug Tools")]
        static void Open()
        {
            GetWindow<DebugToolsWindow>("Debug Tools");
        }

        void OnEnable()
        {
            RefreshWeaponAssets();
            RefreshEnemyPrefabs();
        }

        void OnProjectChange()
        {
            RefreshWeaponAssets();
            RefreshEnemyPrefabs();
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
            weaponNames = new string[weaponAssets.Length];
            for (int i = 0; i < weaponAssets.Length; i++)
                weaponNames[i] = weaponAssets[i].DisplayName;
            selectedWeapon = ClampSelection(selectedWeapon, weaponAssets.Length);
        }

        void RefreshEnemyPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            var found = new List<GameObject>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new System.Exception($"[PickShoot] DebugToolsWindow: 프리팹 로드 실패: {path}");
                if (prefab.GetComponent<EnemyHealth>() != null) found.Add(prefab);
            }
            enemyPrefabs = found.ToArray();
            enemyNames = new string[enemyPrefabs.Length];
            for (int i = 0; i < enemyPrefabs.Length; i++)
                enemyNames[i] = enemyPrefabs[i].name;
            selectedEnemy = ClampSelection(selectedEnemy, enemyPrefabs.Length);
        }

        // 자산이 삭제돼 목록이 줄면 드롭다운 선택이 범위를 벗어나므로 마지막 항목으로 되돌린다.
        static int ClampSelection(int selected, int count)
        {
            return count == 0 ? 0 : Mathf.Clamp(selected, 0, count - 1);
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
            var waves = FindFirstObjectByType<WaveManager>();
            if (waves == null)
                EditorGUILayout.HelpBox("씬에 WaveManager가 없습니다.", MessageType.Warning);
            else
                DrawWave(waves);

            DrawSeparator();

            var experience = FindFirstObjectByType<PlayerExperience>();
            if (experience == null)
                EditorGUILayout.HelpBox("씬에 PlayerExperience가 없습니다.", MessageType.Warning);
            else
                DrawExperience(experience);

            DrawSeparator();

            var weapons = FindFirstObjectByType<PlayerWeapons>();
            if (weapons == null)
            {
                EditorGUILayout.HelpBox("씬에 PlayerWeapons가 없습니다 (플레이어 사망 후에는 파괴됨).", MessageType.Warning);
                return;
            }

            // 적 스폰은 플레이어가 살아 있어야 하므로 (WaveManager.SpawnNow가 거부) 무기 섹션과 같은 조건 뒤에 둔다.
            if (waves != null)
            {
                DrawEnemySpawn(waves);
                DrawSeparator();
            }

            DrawAddWeapon(weapons);
            DrawSeparator();
            DrawWeapons(weapons);
        }

        // 섹션 사이 가로 구분선. 위아래 여백을 두고 한 픽셀 높이의 반투명 선을 그린다.
        static void DrawSeparator()
        {
            EditorGUILayout.Space(SeparatorMargin);
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, SeparatorColor);
            EditorGUILayout.Space(SeparatorMargin);
        }

        // 드롭다운에서 적 종류를 고르고 Spawn — 예고 없이 곧바로 플레이어 주변 스폰 거리에 나온다. 해금 웨이브까지 기다리지 않고
        // 새 적의 행동을 시험하는 용도. 편성·동시 수 상한은 무시하고, 웨이브 종료 정리는 평소대로 받는다.
        void DrawEnemySpawn(WaveManager waves)
        {
            GUILayout.Label("Spawn Enemy", EditorStyles.boldLabel);
            if (enemyPrefabs.Length == 0)
            {
                EditorGUILayout.HelpBox("프로젝트에 EnemyHealth가 붙은 프리팹이 없습니다.", MessageType.Error);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                selectedEnemy = EditorGUILayout.Popup(selectedEnemy, enemyNames);
                if (GUILayout.Button("Spawn", GUILayout.Width(ActionButtonWidth)))
                    waves.SpawnNow(enemyPrefabs[selectedEnemy]);
            }
        }

        // 진행 중인 웨이브를 남은 시간과 무관하게 끝낸다. 시간이 다 된 것과 같은 경로(적 정리 → sweep → 회복 → 카드)를 타므로
        // 카드 화면·리롤·다음 웨이브의 적 해금을 기다리지 않고 본다. 카운트다운·클리어 중에는 끝낼 웨이브가 없어 잠근다.
        void DrawWave(WaveManager waves)
        {
            GUILayout.Label($"Wave {waves.CurrentWave}   {waves.Phase}   {waves.WaveTimeRemaining:0.0}s", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(waves.Phase != WavePhase.Active))
            {
                if (GUILayout.Button("Clear Wave Now"))
                    waves.EndWaveNow();
            }
        }

        // 처치 없이 경험치를 올려 레벨 업(체력 성장·리롤 적립)을 바로 시험한다. 플로팅 텍스트는 띄우지 않는다.
        void DrawExperience(PlayerExperience experience)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"XP {experience.Total}   LV {experience.Level}", EditorStyles.boldLabel);
                if (GUILayout.Button("+10", GUILayout.Width(ActionButtonWidth)))
                    experience.Add(10);
            }
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

            // 최소 1자루는 유지 (Weapons §2 시작 슬롯 1). PlayerWeapons.RemoveLastWeapon도 같은 조건을 검사한다.
            using (new EditorGUI.DisabledScope(slots.Count <= 1))
            {
                if (GUILayout.Button("Remove Last"))
                    weapons.RemoveLastWeapon();
            }
        }

        // 드롭다운에서 무기 종류를 고르고 Add — 그 종류의 새 자루가 다음 슬롯에 들어간다 (Weapons §3, 항상 Lv.1).
        void DrawAddWeapon(PlayerWeapons weapons)
        {
            GUILayout.Label("Add Weapon", EditorStyles.boldLabel);
            if (weaponAssets.Length == 0)
            {
                EditorGUILayout.HelpBox("프로젝트에 WeaponData 자산이 없습니다 (검색: t:WeaponData).", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                selectedWeapon = EditorGUILayout.Popup(selectedWeapon, weaponNames);
                using (new EditorGUI.DisabledScope(!weapons.HasEmptySlot))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(ActionButtonWidth)))
                        weapons.AddWeapon(weaponAssets[selectedWeapon]);
                }
            }
        }
    }
}
