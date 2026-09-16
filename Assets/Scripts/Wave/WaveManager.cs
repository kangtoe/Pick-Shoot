using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 진행 단계. HUD가 배너 문구를 고르고, 스폰 코루틴이 웨이브 종료 후 스폰을 막는 데 쓴다.
    //   Idle: 첫 웨이브 전 / Countdown: 번호는 올랐고 스폰 시작 대기 / Active: 스폰 중 / Cleared: 시간 종료 후 정리·업그레이드 선택 중
    public enum WavePhase { Idle, Countdown, Active, Cleared }

    public class WaveManager : MonoBehaviour
    {
        [System.Serializable]
        public class WaveSpawnEntry
        {
            public GameObject prefab;
            [Min(1)] public int unlockWave = 1;
            [Min(0f)] public float weight = 1f;
        }

        [SerializeField] WaveSpawnEntry[] waveEntries;
        [SerializeField] GameObject spawnWarningPrefab;
        [SerializeField] UpgradeManager upgradeManager;
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(9f, 9f);
        [SerializeField] float baseWaveDuration = 20f;
        [SerializeField] float waveDurationIncrement = 5f;
        [SerializeField] float betweenWaveDelay = 2f;
        [SerializeField] float upgradeDelay = 2f;
        [SerializeField] float spawnInterval = 0.5f;
        [SerializeField] float spawnWarningDuration = 0.6f;
        [SerializeField] float minSpawnDistanceFromPlayer = 5f;
        [SerializeField] int baseMaxEnemies = 8;
        [SerializeField] int maxEnemiesPerWaveIncrement = 2;

        int currentWave;
        float currentWaveDuration;
        float waveTimeRemaining;
        WavePhase phase = WavePhase.Idle;
        int pendingWarnings;
        readonly List<EnemyHealth> spawnedEnemies = new List<EnemyHealth>();
        Transform player;
        PlayerPickup playerPickup;
        PlayerHealth playerHealth;

        public int CurrentWave => currentWave;
        public WavePhase Phase => phase;
        // 현재 웨이브의 전체 지속시간. HUD 타이머 바의 분모.
        public float WaveDuration => currentWaveDuration;
        public float WaveTimeRemaining => waveTimeRemaining;

        void Start()
        {
            if (waveEntries == null || waveEntries.Length == 0)
                throw new System.Exception("[PickShoot] WaveManager.waveEntries 미설정.");
            for (int i = 0; i < waveEntries.Length; i++)
                if (waveEntries[i] == null || waveEntries[i].prefab == null)
                    throw new System.Exception($"[PickShoot] WaveManager.waveEntries[{i}].prefab 미설정.");

            var p = GameObject.FindWithTag("Player");
            if (p == null)
                throw new System.Exception("[PickShoot] Player 태그 오브젝트 없음.");
            player = p.transform;
            playerPickup = p.GetComponent<PlayerPickup>();
            if (playerPickup == null)
                throw new System.Exception("[PickShoot] Player에 PlayerPickup 컴포넌트 없음 (웨이브 클리어 sweep에 필요).");
            playerHealth = p.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] Player에 PlayerHealth 컴포넌트 없음 (웨이브 시작 체력 회복에 필요).");

            StartCoroutine(RunWaves());
        }

        IEnumerator RunWaves()
        {
            while (true)
            {
                if (!CanSpawn()) yield break;
                currentWave++;
                currentWaveDuration = baseWaveDuration + (currentWave - 1) * waveDurationIncrement;
                if (currentWaveDuration <= 0f)
                    throw new System.Exception($"[PickShoot] Wave {currentWave}의 지속시간이 0 이하 ({currentWaveDuration}). baseWaveDuration·waveDurationIncrement 확인.");
                int maxAlive = baseMaxEnemies + (currentWave - 1) * maxEnemiesPerWaveIncrement;
                waveTimeRemaining = currentWaveDuration;
                phase = WavePhase.Countdown;

                yield return new WaitForSeconds(betweenWaveDelay);

                if (!CanSpawn()) yield break;
                // 매 웨이브는 온전한 체력으로 시작한다 (GDD §3.3).
                playerHealth.RestoreFull();

                phase = WavePhase.Active;
                float elapsed = 0f;
                float nextSpawnAt = 0f;
                while (elapsed < currentWaveDuration)
                {
                    if (!CanSpawn()) yield break;
                    if (elapsed >= nextSpawnAt)
                    {
                        if (CountCommittedEnemies() < maxAlive)
                            StartCoroutine(SpawnWithWarning());
                        nextSpawnAt += spawnInterval;
                    }
                    waveTimeRemaining = currentWaveDuration - elapsed;
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                waveTimeRemaining = 0f;
                phase = WavePhase.Cleared;

                DestroyAllEnemies();

                // 웨이브 클리어 연출: 필드에 남은 탄환을 sweep으로 회수한다.
                // upgradeDelay 구간에 병합 — 시각 연출과 카드 등장 사이 리듬.
                yield return playerPickup.SweepAll(upgradeDelay);

                if (!CanSpawn()) yield break;
                if (upgradeManager != null)
                    yield return upgradeManager.PresentAndWait(currentWave);
            }
        }

        // 스폰 가능 조건: 플레이어가 살아 있어야 함. 사망(파괴) 시 웨이브 진행·스폰·업그레이드 UI 중단.
        bool CanSpawn() => player != null;

        IEnumerator SpawnWithWarning()
        {
            var prefab = PickEnemyPrefab();
            if (prefab == null) yield break;

            Vector2 pos = Vector2.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                pos = PickPerimeterPoint();
                if (player == null) break;
                if (Vector2.Distance(pos, player.position) >= minSpawnDistanceFromPlayer)
                    break;
            }

            pendingWarnings++;
            if (spawnWarningPrefab != null)
                Instantiate(spawnWarningPrefab, pos, Quaternion.identity);

            yield return new WaitForSeconds(spawnWarningDuration);
            pendingWarnings--;

            if (phase != WavePhase.Active) yield break;
            if (!CanSpawn()) yield break;

            var enemy = Instantiate(prefab, pos, Quaternion.identity);
            var health = enemy.GetComponent<EnemyHealth>();
            if (health == null)
                throw new System.Exception($"[PickShoot] Enemy 프리팹({prefab.name})에 EnemyHealth 없음.");
            spawnedEnemies.Add(health);
        }

        // 현재 웨이브에서 등장 가능한 프리팹 중 가중치 기반 랜덤 선택.
        // 등장 이후 웨이브에서도 이전 적은 계속 등장한다 (Enemies §5).
        GameObject PickEnemyPrefab()
        {
            float totalWeight = 0f;
            for (int i = 0; i < waveEntries.Length; i++)
            {
                var e = waveEntries[i];
                if (currentWave < e.unlockWave) continue;
                totalWeight += e.weight;
            }
            if (totalWeight <= 0f)
                throw new System.Exception($"[PickShoot] Wave {currentWave}에서 스폰 가능한 적 프리팹이 없음.");

            float pick = Random.value * totalWeight;
            for (int i = 0; i < waveEntries.Length; i++)
            {
                var e = waveEntries[i];
                if (currentWave < e.unlockWave) continue;
                pick -= e.weight;
                if (pick <= 0f) return e.prefab;
            }
            return waveEntries[waveEntries.Length - 1].prefab;
        }

        // 활성 적 + 스폰 대기 중인 워닝 수. 대기 중인 워닝을 포함해야 cap을 순간적으로 초과하지 않음.
        int CountCommittedEnemies()
        {
            for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
                if (spawnedEnemies[i] == null) spawnedEnemies.RemoveAt(i);
            return spawnedEnemies.Count + pendingWarnings;
        }

        void DestroyAllEnemies()
        {
            // Kill()을 통해 EnemyHealth.Die 경로를 재사용해야 사망 이펙트가 스폰된다.
            for (int i = 0; i < spawnedEnemies.Count; i++)
                if (spawnedEnemies[i] != null) spawnedEnemies[i].Kill();
            spawnedEnemies.Clear();
        }

        Vector2 PickPerimeterPoint()
        {
            int edge = Random.Range(0, 4);
            float x = Random.Range(-arenaHalfExtents.x, arenaHalfExtents.x);
            float y = Random.Range(-arenaHalfExtents.y, arenaHalfExtents.y);
            return edge switch
            {
                0 => new Vector2(x,  arenaHalfExtents.y),
                1 => new Vector2(x, -arenaHalfExtents.y),
                2 => new Vector2( arenaHalfExtents.x, y),
                _ => new Vector2(-arenaHalfExtents.x, y),
            };
        }
    }
}
