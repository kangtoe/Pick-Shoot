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
        [SerializeField] SpawnWarningMarker spawnWarningPrefab;
        [SerializeField] UpgradeManager upgradeManager;
        // 스폰 가능 영역(원점 기준 반폭). 아레나 안쪽 면(14)에서 한 칸 들여 적이 울타리에 걸쳐 나오지 않게 한다.
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(13f, 13f);
        [SerializeField] float baseWaveDuration = 20f;
        [SerializeField] float waveDurationIncrement = 5f;
        [SerializeField] float betweenWaveDelay = 2f;
        [SerializeField] float upgradeDelay = 2f;
        [SerializeField] float spawnInterval = 0.5f;
        [SerializeField] float spawnWarningDuration = 0.6f;
        // 스폰 지점은 플레이어로부터 이 거리 범위 안에서 고른다 (Enemies §1).
        // 최소는 등장 직후 피할 여유, 최대는 경고 마커가 카메라 시야(직교 반높이 8.4375) 안에 뜨게 하는 값.
        [SerializeField, Min(0f)] float minSpawnDistanceFromPlayer = 5f;
        [SerializeField] float maxSpawnDistanceFromPlayer = 8f;
        [SerializeField] int baseMaxEnemies = 8;
        [SerializeField] int maxEnemiesPerWaveIncrement = 2;

        // 기각 표집 시도 횟수. 채택률이 절반 안팎이라 정상 설정에서는 사실상 소진되지 않는다.
        const int SpawnPointAttempts = 32;

        int currentWave;
        float currentWaveDuration;
        float waveTimeRemaining;
        WavePhase phase = WavePhase.Idle;
        int pendingWarnings;
        readonly List<EnemyHealth> spawnedEnemies = new List<EnemyHealth>();
        // 예고 중인 마커. 웨이브가 끝나거나 플레이어가 죽으면 남은 예고 시간과 무관하게 함께 치운다.
        readonly List<SpawnWarningMarker> pendingMarkers = new List<SpawnWarningMarker>();
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
            if (spawnWarningPrefab == null)
                throw new System.Exception("[PickShoot] WaveManager.spawnWarningPrefab 미설정.");
            if (maxSpawnDistanceFromPlayer <= minSpawnDistanceFromPlayer)
                throw new System.Exception($"[PickShoot] WaveManager 스폰 거리 범위가 비어 있음 (min {minSpawnDistanceFromPlayer} ≥ max {maxSpawnDistanceFromPlayer}).");

            var p = GameObject.FindWithTag("Player");
            if (p == null)
                throw new System.Exception("[PickShoot] Player 태그 오브젝트 없음.");
            player = p.transform;
            playerPickup = p.GetComponent<PlayerPickup>();
            if (playerPickup == null)
                throw new System.Exception("[PickShoot] Player에 PlayerPickup 컴포넌트 없음 (웨이브 클리어 sweep에 필요).");
            playerHealth = p.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] Player에 PlayerHealth 컴포넌트 없음 (웨이브 클리어 체력 회복에 필요).");

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
                phase = WavePhase.Active;
                float elapsed = 0f;
                float nextSpawnAt = 0f;
                while (elapsed < currentWaveDuration)
                {
                    if (!CanSpawn())
                    {
                        DestroyPendingMarkers();
                        yield break;
                    }
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
                DestroyPendingMarkers();

                // 웨이브 클리어 연출: 필드에 남은 탄환을 sweep으로 회수한다.
                // upgradeDelay 구간에 병합 — 시각 연출과 카드 등장 사이 리듬.
                yield return playerPickup.SweepAll(upgradeDelay);

                if (!CanSpawn()) yield break;
                // 클리어 대기가 끝난 뒤 체력을 완충한다 (GDD §3.3). 시작 시점이 아니라 여기서 회복해야
                // 카운트다운 동안 HUD에 이미 가득 찬 체력이 보이고, 웨이브 내 피해가 다음 웨이브로 넘어가지 않는다.
                playerHealth.RestoreFull();
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

            Vector2 pos = PickSpawnPoint();

            pendingWarnings++;
            var marker = Instantiate(spawnWarningPrefab, pos, Quaternion.identity);
            marker.Begin(spawnWarningDuration);
            pendingMarkers.Add(marker);

            yield return new WaitForSeconds(spawnWarningDuration);
            pendingWarnings--;
            // 웨이브 종료·사망 정리로 이미 치워졌을 수 있다.
            if (marker != null)
            {
                pendingMarkers.Remove(marker);
                Destroy(marker.gameObject);
            }

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

        void DestroyPendingMarkers()
        {
            for (int i = 0; i < pendingMarkers.Count; i++)
                if (pendingMarkers[i] != null) Destroy(pendingMarkers[i].gameObject);
            pendingMarkers.Clear();
        }

        // 스폰 영역 안에서 플레이어와의 거리가 [min, max]인 지점을 면적에 고르게 뽑는다.
        // 바깥 원의 경계 상자를 스폰 영역으로 잘라 그 안에서 뽑고 거리로 거른다 —
        // 각도·거리로 뽑아 영역 밖을 버리는 방식과 달리 플레이어가 구석에 있어도 채택률이 절반 안팎으로 유지된다.
        Vector2 PickSpawnPoint()
        {
            Vector2 center = player.position;
            float max = maxSpawnDistanceFromPlayer;
            float xMin = Mathf.Max(-arenaHalfExtents.x, center.x - max);
            float xMax = Mathf.Min( arenaHalfExtents.x, center.x + max);
            float yMin = Mathf.Max(-arenaHalfExtents.y, center.y - max);
            float yMax = Mathf.Min( arenaHalfExtents.y, center.y + max);
            float minSqr = minSpawnDistanceFromPlayer * minSpawnDistanceFromPlayer;
            float maxSqr = max * max;

            for (int attempt = 0; attempt < SpawnPointAttempts; attempt++)
            {
                var pos = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
                float sqr = (pos - center).sqrMagnitude;
                if (sqr >= minSqr && sqr <= maxSqr) return pos;
            }
            throw new System.Exception($"[PickShoot] 스폰 지점을 찾지 못함 (플레이어 {center}, 거리 {minSpawnDistanceFromPlayer}~{max}, 스폰 영역 ±{arenaHalfExtents}). 거리 범위가 스폰 영역에 비해 너무 큰지 확인.");
        }
    }
}
