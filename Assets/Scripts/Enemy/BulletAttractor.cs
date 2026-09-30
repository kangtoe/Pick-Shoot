using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 수집형 적의 탄환 끌기 (Enemies §6). 매 물리 스텝 반경 안의 정착 탄환에 자기 쪽으로 향하는 속도를 주고, 유지 반경 안에 들어온 탄환은
    // 놓아 둔다 — Bullet의 정착 감속이 멈춰 세워 곁에 머문다. 몸이 움직이면 뒤처진 탄환이 다시 끌려와 따라다닌다.
    // 튕김 중인 탄환은 건드리지 않는다 — 그 페이즈는 속도·충돌을 그대로 유지하는 것이 GDD의 약속이고, 정착하면 그때 끈다.
    // 플레이어 자석에 걸린 탄환은 끌지 않는다 — 자석은 한 번 걸리면 풀리지 않는다(GDD §3.2). 끌려간 탄환은 정착 상태 그대로라 플레이어가
    // 자석 반경까지 다가가면 평소대로 걸리고, 그 순간 여기서는 손을 뗀다. 목록을 들고 있지 않고 매 스텝 반경으로 다시 찾으므로
    // 죽거나 탄환이 멀어져도 따로 놓아줄 것이 없다 — 그 자리에 흩어진 채 남는다.
    // 정착 탄환은 BulletInactive 레이어라 적과 부딪히지 않는다 — 몸 안쪽까지 끌어와도 걸리지 않고, 곁의 탄환이 사격을 막지도 않는다.
    // 플레이어 자석과 달리 콜라이더는 끄지 않는다(벽은 통과하지 못한다) — 끄고 켜는 상태를 두지 않기 위해서다.
    // 반경·속도는 CollectorData 자산이 정한다. 이동은 EnemyWanderer가 맡고 이 컴포넌트는 끌기만 한다.
    public class BulletAttractor : MonoBehaviour
    {
        [SerializeField] CollectorData data;
        [SerializeField] LayerMask bulletLayerMask; // 정착 탄환의 레이어(BulletInactive)

        ContactFilter2D filter;
        static readonly List<Collider2D> hits = new List<Collider2D>();

        void Awake()
        {
            if (data == null)
                throw new System.Exception($"[PickShoot] BulletAttractor.data 미설정 ({name}).");
            if (bulletLayerMask.value == 0)
                throw new System.Exception($"[PickShoot] BulletAttractor.bulletLayerMask 미설정 ({name}). 정착 탄환 레이어(BulletInactive) 필요.");
            if (data.PullRadius <= data.HoldRadius)
                throw new System.Exception($"[PickShoot] CollectorData의 끄는 반경({data.PullRadius})이 유지 반경({data.HoldRadius}) 이하 ({data.name}). 끌 구간이 없다.");
            filter = new ContactFilter2D();
            filter.SetLayerMask(bulletLayerMask);
            filter.useTriggers = false;
        }

        void FixedUpdate()
        {
            Vector2 center = transform.position;
            Physics2D.OverlapCircle(center, data.PullRadius, filter, hits);
            float dt = Time.fixedDeltaTime;
            foreach (var hit in hits)
            {
                var bullet = hit.GetComponent<Bullet>();
                if (bullet == null)
                    throw new System.Exception($"[PickShoot] BulletAttractor: 탄환 레이어의 콜라이더 '{hit.name}'에 Bullet 없음.");
                if (!bullet.IsSettled || bullet.IsMagnetized) continue;

                Vector2 toSelf = center - (Vector2)bullet.transform.position;
                float d = toSelf.magnitude;
                if (d <= data.HoldRadius) continue;

                // 유지 반경을 지나치지 않게 이번 스텝에 남은 거리까지만.
                float speed = Mathf.Min(data.PullSpeed, (d - data.HoldRadius) / dt);
                hit.attachedRigidbody.linearVelocity = toSelf / d * speed;
            }
        }
    }
}
