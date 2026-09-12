using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
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
        float waveTimeRemaining;
        bool waveActive;
        int pendingWarnings;
        readonly List<EnemyHealth> spawnedEnemies = new List<EnemyHealth>();
        Transform player;

        public int CurrentWave => currentWave;
        public float WaveTimeRemaining => waveTimeRemaining;

        void Start()
        {
            if (enemyPrefab == null)
                throw new System.Exception("[PickShoot] WaveManager.enemyPrefab 미설정.");

            var p = GameObject.FindWithTag("Player");
            if (p == null)
                throw new System.Exception("[PickShoot] Player 태그 오브젝트 없음.");
            player = p.transform;

            StartCoroutine(RunWaves());
        }

        IEnumerator RunWaves()
        {
            while (true)
            {
                if (!CanSpawn()) yield break;
                currentWave++;
                float waveDuration = baseWaveDuration + (currentWave - 1) * waveDurationIncrement;
                int maxAlive = baseMaxEnemies + (currentWave - 1) * maxEnemiesPerWaveIncrement;
                waveTimeRemaining = waveDuration;

                yield return new WaitForSeconds(betweenWaveDelay);

                waveActive = true;
                float elapsed = 0f;
                float nextSpawnAt = 0f;
                while (elapsed < waveDuration)
                {
                    if (!CanSpawn())
                    {
                        waveActive = false;
                        yield break;
                    }
                    if (elapsed >= nextSpawnAt)
                    {
                        if (CountCommittedEnemies() < maxAlive)
                            StartCoroutine(SpawnWithWarning());
                        nextSpawnAt += spawnInterval;
                    }
                    waveTimeRemaining = waveDuration - elapsed;
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                waveTimeRemaining = 0f;
                waveActive = false;

                DestroyAllEnemies();

                yield return new WaitForSeconds(upgradeDelay);

                if (!CanSpawn()) yield break;
                if (upgradeManager != null)
                    yield return upgradeManager.PresentAndWait();
            }
        }

        // 스폰 가능 조건: 플레이어가 살아 있어야 함. 사망(파괴) 시 웨이브 진행·스폰·업그레이드 UI 중단.
        bool CanSpawn() => player != null;

        IEnumerator SpawnWithWarning()
        {
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

            if (!waveActive) yield break;
            if (!CanSpawn()) yield break;

            var enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var health = enemy.GetComponent<EnemyHealth>();
            if (health == null)
                throw new System.Exception("[PickShoot] Enemy 프리팹에 EnemyHealth 없음.");
            spawnedEnemies.Add(health);
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

        void OnGUI()
        {
            if (currentWave <= 0) return;
            GUI.Label(new Rect(12, 80, 300, 24), $"Wave {currentWave}   Time {waveTimeRemaining:F1}s");
        }
    }
}
