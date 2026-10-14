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
        // 보스 웨이브: 번호가 bossWaveInterval의 배수인 웨이브는 시작과 함께 보스 한 마리를 예고 후 스폰한다 (Enemies §7).
        [SerializeField] GameObject bossPrefab;
        [SerializeField, Min(1)] int bossWaveInterval = 5;
        [SerializeField] SpawnWarningMarker spawnWarningPrefab;
        [SerializeField] UpgradeManager upgradeManager;
        // 스폰 가능 영역(원점 기준 반폭). 아레나 안쪽 면(14)에서 한 칸 들여 적이 울타리에 걸쳐 나오지 않게 한다.
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(13f, 13f);
        [SerializeField] float baseWaveDuration = 20f;
        [SerializeField] float waveDurationIncrement = 5f;
        [SerializeField] float betweenWaveDelay = 2f;
        [SerializeField] float upgradeDelay = 2f;
        [SerializeField] float spawnInterval = 0.5f;
        // 예고 마커가 떠 있는 시간. 플레이어가 마커를 보고 자리를 옮길 여유 (Enemies §1).
        [SerializeField, Min(0f)] float spawnWarningDuration = 1f;
        // 스폰 지점은 플레이어로부터 이 거리 범위 안에서 고른다 (Enemies §1).
        // 최소는 등장 직후 피할 여유, 최대는 경고 마커가 카메라 시야(직교 반높이 8.4375) 안에 뜨게 하는 값.
        [SerializeField, Min(0f)] float minSpawnDistanceFromPlayer = 5f;
        [SerializeField] float maxSpawnDistanceFromPlayer = 8f;
        // 예고가 끝난 시점에 플레이어가 마커에서 이 거리 안에 있으면 그 스폰은 건너뛴다 — 등장과 동시에 접촉 피해를 주는 스폰 방지.
        // 적·플레이어 콜라이더 반지름 합(약 1.1)에 한 걸음의 여유. 최소 스폰 거리보다 작아야 한다.
        [SerializeField, Min(0f)] float spawnAbortDistance = 2.5f;
        [SerializeField] int baseMaxEnemies = 8;
        [SerializeField] int maxEnemiesPerWaveIncrement = 2;

        // 적 등급 (Enemies §7, 2026-10-14) — 단계마다 HP·이동 속도·접촉 피해·경험치 배율과 틴트, 웨이브에 따른 등장 확률.
        // 확률은 startWave부터 웨이브마다 chancePerWave씩 maxChance까지. 3단계를 먼저 뽑고 그다음 2단계, 나머지는 1단계(EnemyRank.Normal).
        [System.Serializable]
        public class TierSettings
        {
            [Min(0.1f)] public float hpMultiplier = 1f;
            [Min(0.1f)] public float speedMultiplier = 1f;
            [Min(0)] public int damageBonus;
            [Min(0f)] public float expMultiplier = 1f;
            public Color tint = Color.white;
            [Min(1)] public int startWave = 1;
            [Range(0f, 1f)] public float chancePerWave;
            [Range(0f, 1f)] public float maxChance;

            public float Chance(int wave) => wave < startWave ? 0f : Mathf.Min(maxChance, (wave - startWave + 1) * chancePerWave);
        }
        [SerializeField] TierSettings tier2 = new TierSettings { hpMultiplier = 2f, speedMultiplier = 1.15f, expMultiplier = 2f, tint = new Color(1f, 0.85f, 0.55f), startWave = 3, chancePerWave = 0.1f, maxChance = 0.55f };
        [SerializeField] TierSettings tier3 = new TierSettings { hpMultiplier = 4f, speedMultiplier = 1.3f, damageBonus = 1, expMultiplier = 4f, tint = new Color(1f, 0.55f, 0.55f), startWave = 6, chancePerWave = 0.08f, maxChance = 0.4f };
        // 챔피언 (Enemies §7) — 웨이브 스폰의 일반 적에만, 단계 위에 덧붙는다: 크기·HP·접촉 피해·경험치. 보스·소환 졸개는 단계만 받고 챔피언이 되지 않는다.
        [SerializeField, Min(1f)] float championScale = 1.5f;
        [SerializeField, Min(1f)] float championHpMultiplier = 3f;
        [SerializeField, Min(0)] int championDamageBonus = 1;
        [SerializeField, Min(1f)] float championExpMultiplier = 3f;
        [SerializeField, Min(1)] int championStartWave = 3;
        [SerializeField, Range(0f, 1f)] float championChancePerWave = 0.02f;
        [SerializeField, Range(0f, 1f)] float championMaxChance = 0.15f;
        // 스폰 강화 (Enemies §7) — 스폰 간격은 웨이브마다 spawnIntervalDecay배(하한 minSpawnInterval), 한 번에 나오는 수는 spawnGroupEveryWaves 웨이브마다 하나씩 는다.
        [SerializeField, Range(0.5f, 1f)] float spawnIntervalDecay = 0.9f;
        [SerializeField, Min(0.05f)] float minSpawnInterval = 0.25f;
        [SerializeField, Min(1)] int spawnGroupEveryWaves = 3;

        // 기각 표집 시도 횟수. 채택률이 절반 안팎이라 정상 설정에서는 사실상 소진되지 않는다.
        const int SpawnPointAttempts = 32;

        int currentWave;
        float currentWaveDuration;
        float waveTimeRemaining;
        WavePhase phase = WavePhase.Idle;
        // EndWaveNow가 세운다. 진행 중인 웨이브가 남은 시간을 무시하고 다음 프레임에 끝난다.
        bool endWaveRequested;
        int pendingWarnings;
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
            if (bossPrefab == null)
                throw new System.Exception("[PickShoot] WaveManager.bossPrefab 미설정.");
            if (spawnWarningPrefab == null)
                throw new System.Exception("[PickShoot] WaveManager.spawnWarningPrefab 미설정.");
            if (upgradeManager == null)
                throw new System.Exception("[PickShoot] WaveManager.upgradeManager 미설정 (시작 무기 고르기·웨이브 클리어 보상).");
            if (spawnWarningDuration <= 0f)
                throw new System.Exception($"[PickShoot] WaveManager.spawnWarningDuration이 0 이하 ({spawnWarningDuration}).");
            if (maxSpawnDistanceFromPlayer <= minSpawnDistanceFromPlayer)
                throw new System.Exception($"[PickShoot] WaveManager 스폰 거리 범위가 비어 있음 (min {minSpawnDistanceFromPlayer} ≥ max {maxSpawnDistanceFromPlayer}).");
            if (tier2.maxChance + tier3.maxChance > 1f)
                throw new System.Exception($"[PickShoot] WaveManager 2·3단계 최대 확률의 합({tier2.maxChance + tier3.maxChance})이 1을 넘음.");
            if (spawnAbortDistance >= minSpawnDistanceFromPlayer)
                throw new System.Exception($"[PickShoot] WaveManager.spawnAbortDistance({spawnAbortDistance})가 최소 스폰 거리({minSpawnDistanceFromPlayer}) 이상 — 플레이어가 가만히 있어도 스폰이 건너뛰어진다.");

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
            // 첫 웨이브 전에 시작 무기를 고른다 (Weapons §2). 고르기 전에는 자루가 없고 웨이브 번호도 오르지 않는다 (Idle).
            yield return upgradeManager.PresentStartAndWait();

            while (true)
            {
                if (!CanSpawn()) yield break;
                currentWave++;
                currentWaveDuration = baseWaveDuration + (currentWave - 1) * waveDurationIncrement;
                if (currentWaveDuration <= 0f)
                    throw new System.Exception($"[PickShoot] Wave {currentWave}의 지속시간이 0 이하 ({currentWaveDuration}). baseWaveDuration·waveDurationIncrement 확인.");
                int maxAlive = baseMaxEnemies + (currentWave - 1) * maxEnemiesPerWaveIncrement;
                float interval = Mathf.Max(minSpawnInterval, spawnInterval * Mathf.Pow(spawnIntervalDecay, currentWave - 1));
                int group = 1 + (currentWave - 1) / spawnGroupEveryWaves;
                waveTimeRemaining = currentWaveDuration;
                phase = WavePhase.Countdown;

                yield return new WaitForSeconds(betweenWaveDelay);

                if (!CanSpawn()) yield break;
                phase = WavePhase.Active;
                // 보스 웨이브는 시작과 함께 보스를 예고한다. 편성 풀·동시 수 상한과 무관하게 나온다.
                if (currentWave % bossWaveInterval == 0)
                    StartCoroutine(SpawnWithWarning(bossPrefab, mustSpawn: true));
                float elapsed = 0f;
                float nextSpawnAt = 0f;
                while (elapsed < currentWaveDuration && !endWaveRequested)
                {
                    if (!CanSpawn())
                    {
                        DestroyPendingMarkers();
                        yield break;
                    }
                    if (elapsed >= nextSpawnAt)
                    {
                        // 예고가 끝나기 전에 웨이브가 끝나면 스폰되지 않으므로 그런 예고는 아예 띄우지 않는다.
                        bool warningFits = elapsed + spawnWarningDuration <= currentWaveDuration;
                        if (warningFits)
                        {
                            int count = Mathf.Min(group, maxAlive - CountCommittedEnemies());
                            for (int i = 0; i < count; i++)
                                StartCoroutine(SpawnWithWarning(PickEnemyPrefab(), mustSpawn: false));
                        }
                        nextSpawnAt += interval;
                    }
                    waveTimeRemaining = currentWaveDuration - elapsed;
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                endWaveRequested = false;
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
                yield return upgradeManager.PresentAndWait(currentWave);
            }
        }

        // 스폰 가능 조건: 플레이어가 살아 있어야 함. 사망(파괴) 시 웨이브 진행·스폰·업그레이드 UI 중단.
        bool CanSpawn() => player != null;

        // 진행 중인 웨이브를 즉시 끝낸다 (디버그 도구용). 시간이 다 된 것과 같은 경로 — 적 정리 → sweep → 회복 → 카드 — 로 이어진다.
        public void EndWaveNow()
        {
            if (phase != WavePhase.Active)
                throw new System.Exception($"[PickShoot] WaveManager.EndWaveNow: 진행 중인 웨이브가 없음 (phase {phase}).");
            endWaveRequested = true;
        }

        // 예고 없이 곧바로 스폰한다 (디버그 도구용). 편성·해금·동시 수 상한과 무관하게 아무 프리팹이나 플레이어 주변 스폰 거리에 놓고,
        // 목록에 등록하므로 웨이브 종료 정리는 평소대로 받는다. 웨이브가 진행 중이 아니어도 된다 — 해금 전 적을 시험하는 용도.
        public void SpawnNow(GameObject prefab)
        {
            if (prefab == null)
                throw new System.Exception("[PickShoot] WaveManager.SpawnNow: prefab이 null.");
            if (!CanSpawn())
                throw new System.Exception("[PickShoot] WaveManager.SpawnNow: 플레이어가 없음 (사망 후).");
            SpawnEnemy(prefab, PickSpawnPoint());
        }

        // 예고 마커를 띄운 뒤 그 자리에 스폰한다. 예고 중 플레이어가 마커까지 다가왔으면 그 스폰은 건너뛴다 (Enemies §1).
        // 평소 스폰은 건너뛰고 끝이다 — 다음 주기가 새 지점을 고른다. 보스(mustSpawn)는 그 웨이브에 반드시 나와야 하므로 새 지점에 다시 예고한다.
        IEnumerator SpawnWithWarning(GameObject prefab, bool mustSpawn)
        {
            while (true)
            {
                Vector2 pos = PickSpawnPoint();
                yield return Warn(pos);
                if (!WaveStillActive()) yield break;
                if (!PlayerTooClose(pos))
                {
                    // 등급은 스폰 순간의 웨이브로 뽑는다. 보스(mustSpawn)는 단계만, 일반 적은 챔피언도 (Enemies §7).
                    SpawnEnemy(prefab, pos).GetComponent<EnemyHealth>().ApplyRank(RollRank(allowChampion: !mustSpawn));
                    yield break;
                }
                if (!mustSpawn) yield break;
            }
        }

        // 적이 자기 자리에 다른 적을 낳을 때 쓴다 (소환사, Enemies §6). 예고 마커·예고 시간·플레이어가 가까우면 건너뛰는 규칙·웨이브 종료 시 마커 정리가
        // 웨이브 스폰과 같도록 여기서 맡는다 — 마커의 스폰·취소 시점을 아는 쪽은 WaveManager 하나다. 편성·동시 수 상한은 보지 않는다 (서펜트의 몸통과 같음).
        // 예고 중에 owner가 죽으면(파괴) 낳지 않고 알리지도 않는다. 그 밖에는 예고가 끝나면 낳은 적(건너뛰었으면 null)을 onFinished로 돌려준다 —
        // 부른 쪽이 예고 중인 것까지 상한에 세는 데 쓴다.
        public void SummonWithWarning(GameObject prefab, Vector2 pos, Component owner, System.Action<GameObject> onFinished)
        {
            if (prefab == null)
                throw new System.Exception("[PickShoot] WaveManager.SummonWithWarning: prefab이 null.");
            if (owner == null)
                throw new System.Exception("[PickShoot] WaveManager.SummonWithWarning: owner가 null.");
            if (onFinished == null)
                throw new System.Exception("[PickShoot] WaveManager.SummonWithWarning: onFinished가 null.");
            StartCoroutine(SummonWithWarningRoutine(prefab, pos, owner, onFinished));
        }

        IEnumerator SummonWithWarningRoutine(GameObject prefab, Vector2 pos, Component owner, System.Action<GameObject> onFinished)
        {
            yield return Warn(pos);
            if (owner == null) yield break;
            GameObject spawned = WaveStillActive() && !PlayerTooClose(pos) ? SpawnEnemy(prefab, pos) : null;
            if (spawned != null)
            {
                // 낳은 적은 낳은 쪽의 단계를 물려받는다 (Enemies §7).
                var ownerHealth = owner.GetComponent<EnemyHealth>();
                if (ownerHealth == null)
                    throw new System.Exception($"[PickShoot] WaveManager.SummonWithWarning: owner({owner.name})에 EnemyHealth 없음 — 물려줄 등급이 없다.");
                spawned.GetComponent<EnemyHealth>().ApplyRank(TierRank(ownerHealth.Rank.Tier));
            }
            onFinished(spawned);
        }

        // 단계만 입힌 등급 (챔피언 아님, Enemies §7). 보스와 소환 졸개가 쓴다.
        public EnemyRank TierRank(int tier)
        {
            switch (tier)
            {
                case 1: return EnemyRank.Normal;
                case 2: return FromTier(2, tier2);
                case 3: return FromTier(3, tier3);
                default: throw new System.Exception($"[PickShoot] WaveManager.TierRank: 단계 {tier} — 1~3만 있다.");
            }
        }

        static EnemyRank FromTier(int tier, TierSettings s) =>
            new EnemyRank(tier, false, s.hpMultiplier, s.speedMultiplier, s.damageBonus, s.expMultiplier, s.tint, 1f);

        // 지금 웨이브의 확률로 단계를 뽑고, allowChampion이면 챔피언도 뽑아 단계 위에 덧붙인다.
        EnemyRank RollRank(bool allowChampion)
        {
            float r = Random.value;
            float c3 = tier3.Chance(currentWave);
            int tier = r < c3 ? 3 : r < c3 + tier2.Chance(currentWave) ? 2 : 1;
            var rank = TierRank(tier);
            if (!allowChampion || Random.value >= ChampionChance()) return rank;
            return new EnemyRank(rank.Tier, true, rank.HpMultiplier * championHpMultiplier, rank.SpeedMultiplier,
                rank.DamageBonus + championDamageBonus, rank.ExpMultiplier * championExpMultiplier, rank.Tint, championScale);
        }

        float ChampionChance() =>
            currentWave < championStartWave ? 0f : Mathf.Min(championMaxChance, (currentWave - championStartWave + 1) * championChancePerWave);

        // pos에 예고 마커를 띄우고 예고 시간을 기다린 뒤 치운다. 기다리는 동안은 스폰 대기 수에 든다.
        IEnumerator Warn(Vector2 pos)
        {
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
        }

        // 예고가 끝난 지금도 스폰할 웨이브인지: 진행 중이고 플레이어가 살아 있다.
        bool WaveStillActive() => phase == WavePhase.Active && CanSpawn();

        // 예고가 끝난 시점에 플레이어가 마커 가까이 있다 — 등장과 동시에 접촉 피해를 주지 않도록 그 스폰은 건너뛴다.
        bool PlayerTooClose(Vector2 pos) => ((Vector2)player.position - pos).sqrMagnitude < spawnAbortDistance * spawnAbortDistance;

        GameObject SpawnEnemy(GameObject prefab, Vector2 pos)
        {
            var enemy = Instantiate(prefab, pos, Quaternion.identity);
            if (enemy.GetComponent<EnemyHealth>() == null)
                throw new System.Exception($"[PickShoot] Enemy 프리팹({prefab.name})에 EnemyHealth 없음.");
            return enemy;
        }

        // 현재 웨이브에서 등장 가능한 프리팹 중 가중치 기반 랜덤 선택.
        // 등장 이후 웨이브에서도 이전 적은 계속 등장한다 (Enemies §7).
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

        // 살아 있는 적 + 스폰 대기 중인 예고 수(소환사의 졸개 예고 포함). 대기 중인 예고를 포함해야 상한을 순간적으로 넘지 않는다.
        // 스폰 목록 대신 씬의 EnemyHealth를 센다 — 적이 적을 낳는 경우(서펜트의 몸통·분열)도 상한에 들어야 한다. 서펜트는 마디 수만큼 센다.
        int CountCommittedEnemies() => FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length + pendingWarnings;

        // Kill()을 통해 EnemyHealth.Die 경로를 재사용해야 사망 이펙트가 스폰된다.
        void DestroyAllEnemies()
        {
            foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                enemy.Kill();
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
