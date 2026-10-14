using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 수집형 적 (Enemies §6). 흩어진 플레이어 탄환을 찾아가 곁으로 끌어모은다. 이동은 방랑 이동(EnemyWanderer)을 물려받아 찾아갈 탄환이 없을 때 쓴다.
    //   이동: 아레나 어디든 흩어진 탄환(정착했고 자석에 걸리지 않았으며 끄는 범위 밖)이 있으면 가장 가까운 것을 향해 걷는다 — 경로를 가던 중이라도 그 스텝에
    //         바꾼다. 끄는 범위에 들어온 탄환은 끌려오므로 목표에서 빠지고 다음 탄환으로 간다. 흩어진 탄환이 없으면 방랑 이동으로 화면 안을 가로지르는
    //         경로로 움직이고, 탄환을 좇다 놓칠 때마다 그 자리에서 새 경로를 잡는다. 바라보는 방향은 가는 방향이다.
    //   속도: 든 탄환이 많을수록 느리다 — 없으면 데이터의 속도, 기준 수에 이르면 배율까지 든 수에 선형. 많이 훔칠수록 따라잡기 쉽다.
    //   끌기: 매 물리 스텝 끄는 범위 안의 정착 탄환에 자기 쪽으로 향하는 속도를 주어 몸 둘레의 고리(유지 반경) 위에 둔다. 끄는 반경은 몸 가까이라 걸어가서
    //         가져간다. 고리 안의 탄환은 세워 두고, 몸이 움직여 고리 밖으로 밀려나면 다시 끌려와 따라다닌다. 중심은 이 스텝에 옮겨 갈 자리(OnMoved)라 몸에
    //         딸린 것처럼 따른다.
    //   밀어내기: 끌어온 탄환끼리는 간격보다 가까우면 서로 밀어내 겹치지 않는다. 고리 둘레가 간격대로 늘어서기에 모자라면 고리를 키운다 — 고리가 끄는 반경을
    //         넘으면 끄는 범위도 고리 바깥 한 간격까지 넓혀 든 탄환을 놓치지 않는다. Bullet 자신의 분리 힘은 정착 감속에 눌려 약하고 여기서 속도를 덮어쓰므로
    //         따로 계산한다.
    //   튕김 중인 탄환은 건드리지 않는다 — 그 페이즈는 속도·충돌을 그대로 유지하는 것이 GDD의 약속이고, 정착하면 그때 끈다.
    //   플레이어 자석에 걸린 탄환은 끌지 않는다 — 자석은 한 번 걸리면 풀리지 않는다(GDD §3.2). 끌어온 탄환은 정착 상태 그대로라 플레이어가 자석 반경까지
    //   다가가면 평소대로 걸리고 그 순간 손을 뗀다. 목록을 들지 않고 매 스텝 범위로 다시 찾으므로 죽거나 탄환이 멀어져도 따로 놓아줄 것이 없다.
    //   정착 탄환은 BulletInactive 레이어라 적과 부딪히지 않는다 — 몸 안쪽까지 끌어와도 걸리지 않고, 곁의 탄환이 사격을 막지도 않는다.
    //   플레이어 자석과 달리 콜라이더는 끄지 않는다(벽은 통과하지 못한다) — 끄고 켜는 상태를 두지 않기 위해서다.
    //   여럿일 때: 탄환은 끄는 범위에 둔 수집형 중 가장 가까운 것의 소유다. 다른 수집형의 탄환은 흩어진 것으로 치지 않아 찾아가지 않고, 범위가 겹쳐도 끌지
    //   않는다 — 서로의 고리를 좇아 뭉치거나 한 탄환을 양쪽에서 당기지 않게. 살아 있는 수집형을 전부 등록해 두고 매 스텝 가린다.
    // 반경·속도·간격·감속은 CollectorData 자산이 정한다. 넉백·분리 힘·벽 판정·걷기는 공통 골격 그대로.
    public class EnemyCollector : EnemyWanderer
    {
        [SerializeField] LayerMask bulletLayerMask; // 정착 탄환의 레이어(BulletInactive)

        // 아레나 어디든 닿는 탐색 반경. 흩어진 탄환은 거리와 무관하게 찾아간다.
        const float SeekRadius = 100f;

        CollectorData collector;
        ContactFilter2D bulletFilter;
        static readonly List<Collider2D> hits = new List<Collider2D>();
        static readonly List<Rigidbody2D> held = new List<Rigidbody2D>();
        // 살아 있는 수집형 전부. 탄환이 누구 것인지 가리는 데 쓴다.
        static readonly List<EnemyCollector> all = new List<EnemyCollector>();
        // 지난 스텝에 탄환을 좇고 있었는지. 놓치는 순간 새 경로를 잡기 위해 기억한다.
        bool seeking;
        // 지난 스텝의 고리 반지름과 든 탄환 수. 끄는 범위와 이동 속도가 이를 따른다.
        float ring;
        int heldCount;

        protected override void Awake()
        {
            base.Awake();
            collector = Data as CollectorData;
            if (collector == null)
                throw new System.Exception($"[PickShoot] EnemyCollector.data가 CollectorData 자산이 아님 ({name}: {Data.name}).");
            if (bulletLayerMask.value == 0)
                throw new System.Exception($"[PickShoot] EnemyCollector.bulletLayerMask 미설정 ({name}). 정착 탄환 레이어(BulletInactive) 필요.");
            if (collector.PullRadius <= collector.HoldRadius)
                throw new System.Exception($"[PickShoot] CollectorData의 끄는 반경({collector.PullRadius})이 유지 반경({collector.HoldRadius}) 이하 ({Data.name}). 끌 구간이 없다.");
            bulletFilter = new ContactFilter2D();
            bulletFilter.SetLayerMask(bulletLayerMask);
            bulletFilter.useTriggers = false;
            ring = collector.HoldRadius;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        // 끄는 범위. 고리가 커져 끄는 반경을 넘으면 고리 바깥 한 간격까지 — 든 탄환이 고리 위에서 빠져나가지 않게.
        float CaptureRadius => Mathf.Max(collector.PullRadius, ring + collector.BulletSpacing);

        // 든 탄환 수에 따른 이동 속도 배율. 없으면 1, 기준 수 이상이면 데이터의 배율.
        float LoadSpeedFactor => Mathf.Lerp(1f, collector.LoadedSpeedMultiplier, Mathf.Clamp01((float)heldCount / collector.SlowdownBullets));

        protected override Vector2 Steer()
        {
            float factor = LoadSpeedFactor;
            if (TryFindLooseBullet(out Vector2 bulletPosition))
            {
                seeking = true;
                Vector2 delta = bulletPosition - Body.position;
                Face(delta.x);
                if (delta.sqrMagnitude <= 0.0001f) return Vector2.zero;
                return delta.normalized * (MoveSpeed * factor);
            }
            if (seeking)
            {
                seeking = false;
                TakeNewPath();
            }
            return SteerAlongPath() * factor;
        }

        // 이 자리의 탄환을 끄는 범위에 둔 수집형 중 가장 가까운 것. 없으면 null — 아무도 들지 않은 흩어진 탄환.
        static EnemyCollector ClaimantOf(Vector2 position)
        {
            EnemyCollector best = null;
            float bestSqr = float.MaxValue;
            foreach (var c in all)
            {
                float sqr = (position - c.Body.position).sqrMagnitude;
                float capture = c.CaptureRadius;
                if (sqr > capture * capture || sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = c;
            }
            return best;
        }

        // 가장 가까운 흩어진 탄환 — 정착했고 자석에 걸리지 않았으며 어느 수집형의 끄는 범위에도 들지 않은 것. 거리 제한은 없다.
        bool TryFindLooseBullet(out Vector2 position)
        {
            position = default;
            Physics2D.OverlapCircle(Body.position, SeekRadius, bulletFilter, hits);
            float bestSqr = float.MaxValue;
            foreach (var hit in hits)
            {
                if (!IsCollectible(hit)) continue;
                Vector2 p = hit.attachedRigidbody.position;
                if (ClaimantOf(p) != null) continue;
                float sqr = (p - Body.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                position = p;
            }
            return bestSqr < float.MaxValue;
        }

        // 탄환 레이어의 콜라이더가 끌 수 있는 탄환인지. 튕김 중이거나 플레이어 자석에 걸린 것은 아니다.
        bool IsCollectible(Collider2D hit)
        {
            var bullet = hit.GetComponent<Bullet>();
            if (bullet == null)
                throw new System.Exception($"[PickShoot] EnemyCollector: 탄환 레이어의 콜라이더 '{hit.name}'에 Bullet 없음.");
            return bullet.IsSettled && !bullet.IsMagnetized;
        }

        protected override void OnMoved(Vector2 to) => PullBullets(to);

        void PullBullets(Vector2 center)
        {
            Physics2D.OverlapCircle(center, CaptureRadius, bulletFilter, hits);
            held.Clear();
            foreach (var hit in hits)
            {
                if (!IsCollectible(hit)) continue;
                var rb = hit.attachedRigidbody;
                // 다른 수집형에 더 가까운 탄환은 그쪽 것이다.
                if (ClaimantOf(rb.position) != this) continue;
                held.Add(rb);
            }
            heldCount = held.Count;

            // 고리 반지름: 탄환이 간격대로 늘어서기에 둘레가 모자라면 키운다.
            float spacing = collector.BulletSpacing;
            ring = Mathf.Max(collector.HoldRadius, heldCount * spacing / (2f * Mathf.PI));
            float maxSpeed = collector.PullSpeed;
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < held.Count; i++)
            {
                var rb = held[i];
                Vector2 pos = rb.position;
                Vector2 velocity = Vector2.zero;

                // 고리 밖이면 고리까지만 끌어당긴다 — 지나치지 않게 이번 스텝의 남은 거리로 제한.
                Vector2 toCenter = center - pos;
                float d = toCenter.magnitude;
                if (d > ring) velocity = toCenter / d * Mathf.Min(maxSpeed, (d - ring) / dt);

                // 간격보다 가까운 이웃에게서 겹친 만큼의 절반씩 물러난다 (양쪽이 각자 절반).
                for (int j = 0; j < held.Count; j++)
                {
                    if (j == i) continue;
                    Vector2 away = pos - held[j].position;
                    float od = away.magnitude;
                    if (od >= spacing) continue;
                    // 정확히 겹친 둘은 방향이 없으므로 순서로 좌우를 가른다.
                    Vector2 dir = od > 0.0001f ? away / od : (i < j ? Vector2.right : Vector2.left);
                    velocity += dir * ((spacing - od) * 0.5f / dt);
                }

                // 밀어내기가 여럿 겹쳐 튀지 않게 끄는 속도까지로 묶는다.
                if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
                    velocity = velocity.normalized * maxSpeed;
                rb.linearVelocity = velocity;
            }
        }
    }
}
