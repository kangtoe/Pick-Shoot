using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 소환사형 적 (Enemies §6). 방랑 이동(EnemyWanderer)으로 화면 안을 가로지르며 졸개를 두 가지로 낳는다.
    //   쏘기: 일정 주기마다 그 순간 서 있는 자리에 웨이브 스폰과 같은 예고 마커를 띄우고, 예고가 끝나면 거기에 쏘는 졸개(EnemyDart — 나온 순간의 플레이어 방향으로
    //         직진하다 벽·화면 가장자리에서 죽는 발사체 같은 적)를 하나 낳는다. 예고·스폰은 WaveManager.SummonWithWarning에 맡긴다 — 마커, 예고 시간, 플레이어가 마커
    //         가까이 있으면 건너뛰는 규칙, 웨이브 종료 시 마커 정리가 웨이브 스폰과 같다. 소환사는 예고 중에도 계속 가므로 졸개는 소환사가 지나온 자리에 나타난다.
    //         스폰 뒤 한 주기가 지나야 처음 예고한다 — 등장을 보고 대처할 여유. 이후 매 주기 하나씩. 살아 있는 것과 예고 중인 것을 합쳐 상한이면 그 주기는 건너뛴다
    //         (쌓아 두지 않는다). 상한은 소환사마다 따로 센다. 예고 중에 소환사가 죽으면 그 졸개는 나오지 않는다 — 소환사를 먼저 잡을 이유.
    //   죽음: 플레이어에게 죽으면(EnemyHealth.Slain) 그 자리 둘레에 일반 졸개(같은 그림의 보통 추적형, Enemy_Minion)를 데이터의 수만큼 놓는다 — 잡은 뒤 뒷정리가
    //         남아 거리를 두고 잡을 이유. 예고 없이 바로 나온다 — 그 자리에는 방금까지 소환사의 몸이 있어 플레이어가 겹쳐 있을 수 없다. 웨이브 종료 정리(Kill)로
    //         죽을 때는 낳지 않는다 — 정리가 지나간 뒤 남는 적이 생긴다.
    //   졸개는 모두 독립된 적이다 — 소환사가 죽어도 남고, 웨이브 종료 정리와 동시 수 상한(씬의 EnemyHealth 수)에 평소대로 든다. 소환사는 그 상한과 무관하게 낳는다
    //         (서펜트의 몸통과 같음) — 쏘는 졸개는 스스로 금방 죽으므로 소환사 자신의 상한은 안전장치에 가깝다.
    //   플레이어가 없으면(사망) 낳지 않는다. 소환사 자신은 느낌표(공격 임박 경고)를 띄우지 않는다 — 공격이 아니라 스폰이고, 마커가 이미 자리를 알린다.
    // 졸개 프리팹은 이 컴포넌트가, 주기·상한·죽을 때의 수는 SummonerData 자산이 정한다. 넉백·분리 힘·벽 판정·걷기·바라보는 방향은 방랑 이동 그대로.
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemySummoner : EnemyWanderer
    {
        [SerializeField] GameObject dartPrefab;   // 주기마다 쏘는 졸개 (EnemyDart)
        [SerializeField] GameObject minionPrefab; // 죽을 때 둘레에 놓는 일반 졸개

        // 죽을 때 일반 졸개를 놓는 반지름 (월드 유닛). 정확히 겹치면 분리 힘이 방향을 못 잡으므로 둘레에 고르게 벌려 둔다.
        const float DeathMinionRadius = 0.4f;

        SummonerData summoner;
        WaveManager waves;
        // 쏜 졸개. 죽어 파괴된 것은 null이 되므로 셀 때 걷어 낸다. 예고 중인 것은 따로 세어 함께 상한에 넣는다.
        readonly List<EnemyHealth> darts = new List<EnemyHealth>();
        int pendingDarts;
        float nextSummonAt;

        protected override void Awake()
        {
            base.Awake();
            summoner = Data as SummonerData;
            if (summoner == null)
                throw new System.Exception($"[PickShoot] EnemySummoner.data가 SummonerData 자산이 아님 ({name}: {Data.name}).");
            if (dartPrefab == null)
                throw new System.Exception($"[PickShoot] EnemySummoner.dartPrefab 미설정 ({name}).");
            if (dartPrefab.GetComponent<EnemyDart>() == null)
                throw new System.Exception($"[PickShoot] EnemySummoner.dartPrefab({dartPrefab.name})에 EnemyDart 없음 ({name}). 쏘는 졸개여야 한다.");
            if (minionPrefab == null)
                throw new System.Exception($"[PickShoot] EnemySummoner.minionPrefab 미설정 ({name}).");
            if (minionPrefab.GetComponent<EnemyHealth>() == null)
                throw new System.Exception($"[PickShoot] EnemySummoner.minionPrefab({minionPrefab.name})에 EnemyHealth 없음 ({name}).");
            if (minionPrefab.GetComponent<EnemySummoner>() != null)
                throw new System.Exception($"[PickShoot] EnemySummoner.minionPrefab({minionPrefab.name})이 소환사 ({name}). 죽을 때 소환사를 낳으면 끝나지 않는다.");
            GetComponent<EnemyHealth>().Slain += OnSlain;
        }

        protected override void Start()
        {
            base.Start();
            waves = FindFirstObjectByType<WaveManager>();
            if (waves == null)
                throw new System.Exception($"[PickShoot] EnemySummoner: 씬에 WaveManager 없음 ({name}). 졸개 예고·스폰에 필요.");
            nextSummonAt = Time.time + summoner.SummonInterval;
        }

        // 경로 이동은 방랑 이동 그대로. 소환 주기도 물리 스텝에서 세어 그 스텝의 몸 자리(Body.position)에 예고를 띄운다.
        protected override Vector2 Steer()
        {
            if (Time.time >= nextSummonAt)
            {
                nextSummonAt += summoner.SummonInterval;
                TrySummon();
            }
            return SteerAlongPath();
        }

        void TrySummon()
        {
            if (Target == null) return;
            darts.RemoveAll(d => d == null);
            if (darts.Count + pendingDarts >= summoner.MaxDarts) return;
            pendingDarts++;
            waves.SummonWithWarning(dartPrefab, Body.position, this, OnSummonFinished);
        }

        // 예고가 끝났다. 건너뛰었으면(플레이어가 마커 가까이·웨이브 종료) null. 나온 졸개는 스스로 플레이어를 겨눈다.
        void OnSummonFinished(GameObject dart)
        {
            pendingDarts--;
            if (dart != null) darts.Add(dart.GetComponent<EnemyHealth>());
        }

        // 플레이어에게 죽었다. 일반 졸개를 둘레에 고르게 놓는다.
        void OnSlain()
        {
            int count = summoner.DeathMinions;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                Vector2 pos = Body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * DeathMinionRadius;
                // 졸개는 소환사의 단계를 물려받는다 (Enemies §7). 쏘는 졸개는 WaveManager.SummonWithWarning이 같은 규칙으로 입힌다.
                var minion = Instantiate(minionPrefab, pos, Quaternion.identity);
                minion.GetComponent<EnemyHealth>().ApplyRank(waves.TierRank(GetComponent<EnemyHealth>().Rank.Tier));
            }
        }
    }
}
