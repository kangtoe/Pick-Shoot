using System.Collections;
using UnityEngine;

namespace PickShoot
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(9f, 9f);
        [SerializeField] int baseEnemies = 3;
        [SerializeField] int enemiesPerWaveIncrement = 2;
        [SerializeField] float betweenWaveDelay = 2f;
        [SerializeField] float spawnInterval = 0.5f;
        [SerializeField] float minSpawnDistanceFromPlayer = 5f;

        int currentWave;
        int enemiesToSpawn;
        int enemiesKilled;
        Transform player;

        public int CurrentWave => currentWave;
        public int KillsThisWave => enemiesKilled;
        public int EnemiesInWave => enemiesToSpawn;

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
                currentWave++;
                enemiesToSpawn = baseEnemies + (currentWave - 1) * enemiesPerWaveIncrement;
                enemiesKilled = 0;

                yield return new WaitForSeconds(betweenWaveDelay);

                for (int i = 0; i < enemiesToSpawn; i++)
                {
                    SpawnOne();
                    yield return new WaitForSeconds(spawnInterval);
                }

                while (enemiesKilled < enemiesToSpawn)
                    yield return null;
            }
        }

        void SpawnOne()
        {
            Vector2 pos = Vector2.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                pos = PickPerimeterPoint();
                if (Vector2.Distance(pos, player.position) >= minSpawnDistanceFromPlayer)
                    break;
            }

            var enemyGO = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var health = enemyGO.GetComponent<EnemyHealth>();
            if (health == null)
                throw new System.Exception("[PickShoot] Enemy 프리팹에 EnemyHealth 없음.");
            health.Died += OnEnemyDied;
        }

        void OnEnemyDied(EnemyHealth _)
        {
            enemiesKilled++;
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
            GUI.Label(new Rect(12, 80, 300, 24), $"Wave {currentWave}   Kills {enemiesKilled}/{enemiesToSpawn}");
        }
    }
}
