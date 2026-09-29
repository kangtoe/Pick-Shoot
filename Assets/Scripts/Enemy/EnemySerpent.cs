using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 서펜트형 보스 (Enemies §5). 표정 있는 네모(머리) 하나가 플레이어를 쫓고, 표정 없는 네모(몸통) 마디들이 머리가 지나온 궤적(SerpentTrail)을
    // 따라 일정 간격으로 잇는다. 마디마다 이 컴포넌트가 붙고 머리일 때만 켜져 EnemyMover로 조향한다. 몸통 마디는 꺼진 채 머리가 매 스텝 자리를 옮긴다.
    //   조향: 플레이어 방향으로 초당 최대 turnRate만큼만 방향을 틀며 늘 이동 속도로 나아간다 — 멈춰 서지 않고 크게 돌아 몸이 넓게 쓸린다.
    //   넉백·분리 힘은 받지 않는다 — 몸이 궤적에 묶여 있어 머리만 밀리면 사슬이 어긋난다. 벽은 공통 골격이 막고, 몸은 머리가 지난 길 위에만 있다.
    //   마디는 각자 EnemyHealth로 따로 맞고 따로 죽는다. 머리가 죽으면 다음 마디가 머리가 되고(분열 없음), 몸통이 죽으면 그 뒤가 떨어져 나가
    //   첫 마디를 새 머리로 하는 별개의 서펜트가 된다(분열). 꼬리가 죽으면 짧아질 뿐이다. 승격된 머리는 HP를 그대로 가져간다.
    //   갓 스폰된 서펜트는 궤적이 스폰 지점 하나뿐이라 몸통이 전부 그 자리에 겹쳐 있다가 머리가 나아가는 만큼 풀려 나온다 — 벽 너머로 몸이 걸치지 않는다.
    // 몸통은 머리가 자기 자신(프리팹 인스턴스)을 복제해 만든다. 복제본은 곧바로 몸통으로 바꿔 Start가 돌지 않으므로 다시 몸통을 낳지 않는다.
    // 그리기 순서는 머리가 다른 적과 같고 몸통은 한 단계 아래라 머리가 늘 몸통 위에 온다. 몸통끼리는 다른 적끼리와 같은 규칙으로 겹친다.
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(SpriteWalkCycle))]
    public class EnemySerpent : EnemyMover
    {
        [SerializeField] Sprite bodySprite; // 몸통 마디의 그림. 머리 그림(2프레임)은 SpriteWalkCycle이 보관한다

        SerpentData serpent;
        SpriteWalkCycle walkCycle;
        int baseSortingOrder;
        // 속한 서펜트의 머리. 머리 자신이면 this.
        EnemySerpent head;
        // 머리일 때만: 뒤따르는 몸통(가까운 순)과 궤적. 몸통일 때는 null.
        List<EnemySerpent> followers;
        SerpentTrail trail;
        Vector2 heading;
        // 승격된 머리는 몸통을 넘겨받았으므로 Start에서 새로 낳지 않는다.
        bool promoted;

        protected override bool UseSeparation => false;

        protected override void Awake()
        {
            base.Awake();
            serpent = Data as SerpentData;
            if (serpent == null)
                throw new System.Exception($"[PickShoot] EnemySerpent.data가 SerpentData 자산이 아님 ({name}: {Data.name}).");
            if (bodySprite == null)
                throw new System.Exception($"[PickShoot] EnemySerpent.bodySprite 미설정 ({name}).");
            walkCycle = GetComponent<SpriteWalkCycle>();
            baseSortingOrder = Sprite.sortingOrder;
            GetComponent<EnemyHealth>().Died += OnDied;
            // 몸통이 없는 머리로 시작한다. 몸통이 되면 BecomeBody가, 승격되면 Promote가 바꾼다.
            head = this;
            followers = new List<EnemySerpent>();
            trail = new SerpentTrail(Body.position);
            heading = Vector2.right;
        }

        protected override void Start()
        {
            base.Start();
            if (promoted) return;
            // 갓 스폰된 머리. 플레이어 쪽을 보고, 몸통을 낳아 모두 이 자리에 겹쳐 둔다.
            heading = InitialHeading();
            for (int i = 0; i < serpent.SegmentCount; i++)
            {
                var segment = Instantiate(gameObject, Body.position, Quaternion.identity).GetComponent<EnemySerpent>();
                segment.name = name;
                segment.BecomeBody(this);
                followers.Add(segment);
            }
            RefreshSortingOrders();
        }

        // 스폰 시의 진행 방향: 플레이어 쪽. 플레이어가 없거나 겹쳐 있으면 오른쪽.
        Vector2 InitialHeading()
        {
            if (Target == null) return Vector2.right;
            Vector2 delta = (Vector2)Target.position - Body.position;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        }

        // 넉백은 받지 않는다 (몸이 궤적에 묶여 있다). 몸통 마디도 꺼진 이 컴포넌트로 같은 호출을 받으므로 여기서 함께 거른다.
        public override void ApplyKnockback(Vector2 velocity) { }

        protected override Vector2 Steer()
        {
            if (Target == null) return Vector2.zero;
            Vector2 delta = (Vector2)Target.position - Body.position;
            if (delta.sqrMagnitude > 0.0001f)
            {
                float maxTurn = serpent.TurnRate * Time.fixedDeltaTime;
                float turn = Mathf.Clamp(Vector2.SignedAngle(heading, delta), -maxTurn, maxTurn);
                heading = Rotate(heading, turn).normalized;
            }
            Face(heading.x);
            return heading * Data.MoveSpeed;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        // 머리가 옮겨 갈 자리가 정해졌다. 궤적을 잇고 몸통을 그 위에 놓는다. 몸통 i(0부터)는 머리에서 (i+1)×간격 뒤.
        protected override void OnMoved(Vector2 to)
        {
            trail.Record(to);
            trail.Trim(followers.Count * serpent.SegmentSpacing);
            for (int i = 0; i < followers.Count; i++)
                followers[i].Body.MovePosition(trail.PointBehind((i + 1) * serpent.SegmentSpacing));
        }

        // 머리 owner의 몸통이 된다. 조향을 멈추고 자리는 머리가 정한다.
        void BecomeBody(EnemySerpent owner)
        {
            head = owner;
            followers = null;
            trail = null;
            enabled = false;
            walkCycle.enabled = false;
            Sprite.sprite = bodySprite;
            Sprite.flipX = false;
        }

        // 몸통이 머리가 된다. 넘겨받은 몸통·궤적으로 이어서 나아간다. HP는 그대로.
        void Promote(List<EnemySerpent> newFollowers, SerpentTrail newTrail, Vector2 newHeading)
        {
            head = this;
            followers = newFollowers;
            trail = newTrail;
            heading = newHeading;
            promoted = true;
            for (int i = 0; i < followers.Count; i++) followers[i].head = this;
            walkCycle.enabled = true;
            Sprite.sprite = walkCycle.IdleFrame;
            Face(heading.x);
            enabled = true;
            RefreshSortingOrders();
        }

        void OnDied() => head.OnSegmentDied(this);

        // 사슬의 마디 하나(머리 자신 포함)가 죽었다. 죽은 마디 뒤쪽을 떼어 새 서펜트로 보낸다.
        void OnSegmentDied(EnemySerpent dead)
        {
            int index = dead == this ? 0 : followers.IndexOf(dead) + 1; // 사슬 속 번호 (머리 0, 몸통 j는 j+1)
            if (dead != this && index == 0)
                throw new System.Exception($"[PickShoot] EnemySerpent: 죽은 마디가 이 서펜트의 몸통 목록에 없음 ({dead.name} → {name}).");

            // 죽은 마디 뒤(followers[index..])는 떨어져 나가고, 죽은 마디부터는 목록에서 뺀다.
            var detached = followers.GetRange(index, followers.Count - index);
            int keep = Mathf.Max(0, index - 1);
            followers.RemoveRange(keep, followers.Count - keep);

            if (detached.Count > 0)
            {
                var newHead = detached[0];
                detached.RemoveAt(0);
                // 새 머리는 죽은 마디 바로 뒤 — 이 머리에서 (index+1)×간격 뒤. 궤적이 향하던 방향으로 계속 간다.
                var newTrail = trail.Split((index + 1) * serpent.SegmentSpacing, out _, out Vector2 direction);
                direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : heading;
                newHead.Promote(detached, newTrail, direction);
            }
            if (dead != this) RefreshSortingOrders();
        }

        void RefreshSortingOrders()
        {
            Sprite.sortingOrder = baseSortingOrder;
            for (int i = 0; i < followers.Count; i++)
                followers[i].Sprite.sortingOrder = baseSortingOrder - 1;
        }
    }
}
