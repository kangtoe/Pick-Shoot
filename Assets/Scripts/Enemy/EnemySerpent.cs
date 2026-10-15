using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PickShoot
{
    // 서펜트형 보스 (Enemies §5). 표정 있는 네모(머리) 하나가 플레이어를 쫓고, 표정 없는 네모(몸통) 마디들이 머리가 지나온 궤적(SerpentTrail)을
    // 따라 일정 간격으로 잇는다. 마디마다 이 컴포넌트가 붙고 머리일 때만 켜져 EnemyMover로 조향한다. 몸통 마디는 꺼진 채 머리가 매 스텝 자리를 옮긴다.
    //   조향: 플레이어 방향으로 초당 최대 turnRate만큼만 방향을 틀며 늘 이동 속도로 나아간다 — 멈춰 서지 않고 크게 돌아 몸이 넓게 쓸린다.
    //   남은 마디가 적을수록 빠르다 — 몸통이 다 있으면 데이터의 속도, 머리만 남으면 그 배율(SerpentData)까지 몸통 수에 선형. 회전 속도는 그대로라 빠를수록 크게 돈다.
    //   넉백은 받지 않는다(보스). 분리 힘은 머리가 자기 사슬 밖의 것(다른 서펜트·다른 적)에게서만 받는다 — 자기 몸에 밀리면 원을 그릴 때 튕겨 나간다.
    //   머리가 어디로 밀리든 궤적은 실제 지난 길이므로 사슬은 어긋나지 않는다. 벽은 공통 골격이 막고, 몸은 머리가 지난 길 위에만 있다.
    //   마디는 모두 뚫을 수 없는 몸(IsSolid)이다 — 겹친 적은 분리 힘과 별개로 겹친 만큼 바로 밀려나, 지나가는 몸통이 적을 밀어젖힌다.
    //   마디는 각자 EnemyHealth로 따로 맞고 따로 죽는다. 머리가 죽으면 다음 마디가 머리가 되고(분열 없음), 몸통이 죽으면 그 뒤가 떨어져 나가
    //   첫 마디를 새 머리로 하는 별개의 서펜트가 된다(분열). 꼬리가 죽으면 짧아질 뿐이다. 승격된 머리는 HP를 그대로 가져간다.
    //   갓 스폰된 서펜트는 궤적이 스폰 지점 하나뿐이라 몸통이 전부 그 자리에 겹쳐 있다가 머리가 나아가는 만큼 풀려 나온다 — 벽 너머로 몸이 걸치지 않는다.
    // 몸통은 머리가 자기 자신(프리팹 인스턴스)을 복제해 만든다. 복제본은 곧바로 몸통으로 바꿔 Start가 돌지 않으므로 다시 몸통을 낳지 않는다.
    // 그리기: 사슬 하나를 SortingGroup 루트 아래 묶어 한 덩어리로 그린다. 그룹은 프리팹의 정렬 순서(다른 적과 같음)를 갖고 루트는 머리 자리를 따라가므로
    //   다른 적과는 머리 자리로 겨루고, 그룹 안에서는 머리 → 첫 몸통 → … → 꼬리 순으로 앞 마디가 뒤 마디 위에 온다. 분열하면 새 머리가 새 루트를 만든다.
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(SpriteWalkCycle))]
    public class EnemySerpent : EnemyMover
    {
        [SerializeField] Sprite bodySprite; // 몸통 마디의 그림. 머리 그림(2프레임)은 SpriteWalkCycle이 보관한다

        SerpentData serpent;
        SpriteWalkCycle walkCycle;
        int groupSortingOrder; // 프리팹이 준 정렬 순서. 사슬 그룹이 다른 적과 겨루는 순서로 쓴다
        // 속한 서펜트의 머리. 머리 자신이면 this.
        EnemySerpent head;
        // 머리일 때만: 뒤따르는 몸통(가까운 순), 궤적, 사슬을 한 덩어리로 그리는 루트(SortingGroup). 몸통일 때는 null.
        List<EnemySerpent> followers;
        SerpentTrail trail;
        Transform chainRoot;
        Vector2 heading;
        // 승격된 머리는 몸통을 넘겨받았으므로 Start에서 새로 낳지 않는다.
        bool promoted;

        protected override void Awake()
        {
            base.Awake();
            serpent = Data as SerpentData;
            if (serpent == null)
                throw new System.Exception($"[PickShoot] EnemySerpent.data가 SerpentData 자산이 아님 ({name}: {Data.name}).");
            if (bodySprite == null)
                throw new System.Exception($"[PickShoot] EnemySerpent.bodySprite 미설정 ({name}).");
            walkCycle = GetComponent<SpriteWalkCycle>();
            groupSortingOrder = Sprite.sortingOrder;
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
            // 몸통은 머리의 등급(보스는 노멀에 웨이브 강화만, Enemies §7)을 물려받는다 — 복제는 HP 같은 런타임 값을 옮기지 않으므로 명시적으로 입힌다.
            // 보스 등급의 크기는 1이다 — 복제가 이미 커진 크기를 옮겨 크기가 두 번 곱해지는 일이 없다.
            var headRank = GetComponent<EnemyHealth>().Rank;
            for (int i = 0; i < serpent.SegmentCount; i++)
            {
                var segment = Instantiate(gameObject, Body.position, Quaternion.identity).GetComponent<EnemySerpent>();
                segment.name = name;
                segment.GetComponent<EnemyHealth>().ApplyRank(headRank);
                segment.BecomeBody(this);
                followers.Add(segment);
            }
            BuildChain();
        }

        // 스폰 시의 진행 방향: 플레이어 쪽. 플레이어가 없거나 겹쳐 있으면 오른쪽.
        Vector2 InitialHeading()
        {
            if (Target == null) return Vector2.right;
            Vector2 delta = (Vector2)Target.position - Body.position;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        }

        // 넉백은 받지 않는다 (보스). 몸통 마디도 꺼진 이 컴포넌트로 같은 호출을 받으므로 여기서 함께 거른다.
        public override void ApplyKnockback(Vector2 velocity) { }

        // 자기 사슬의 마디와는 분리 힘을 주고받지 않는다 (헤더 참조). 다른 서펜트·다른 적에게는 평소대로 밀린다 (쏘는 졸개는 기본 규칙대로 뺀다).
        protected override bool SeparatesFrom(Collider2D other) =>
            base.SeparatesFrom(other) && !(other.TryGetComponent(out EnemySerpent segment) && segment.head == head);

        // 마디는 뚫을 수 없는 몸이다 (헤더 참조). 몸통 마디도 꺼진 이 컴포넌트로 읽히므로 함께 해당한다.
        protected override bool IsSolid => true;

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
            return heading * MoveSpeed * SpeedMultiplier();
        }

        // 남은 마디가 적을수록 빠르다: 몸통이 스폰 수만큼 있으면 1배, 머리만 남으면 배율만큼, 사이는 몸통 수에 선형. 분열체는 짧은 만큼 처음부터 빠르다.
        float SpeedMultiplier() =>
            Mathf.Lerp(serpent.LoneHeadSpeedMultiplier, 1f, (float)followers.Count / serpent.SegmentCount);

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        // 머리가 옮겨 갈 자리가 정해졌다. 궤적을 잇고 몸통을 그 위에 놓은 뒤 사슬 루트를 머리 자리로 옮긴다. 몸통 i(0부터)는 머리에서 (i+1)×간격 뒤.
        protected override void OnMoved(Vector2 to)
        {
            trail.Record(to);
            trail.Trim(followers.Count * serpent.SegmentSpacing);
            for (int i = 0; i < followers.Count; i++)
                followers[i].Body.MovePosition(trail.PointBehind((i + 1) * serpent.SegmentSpacing));
            MoveChainRoot(to);
        }

        // 루트를 머리 자리에 둔다 — 그룹은 루트 자리로 다른 렌더러와 겨룬다. 마디의 자리는 몸(Rigidbody2D)이 갖고 있는데 루트가 움직이면 자식
        // 트랜스폼이 딸려 가고 물리 동기화가 몸을 그만큼 옮겨 버리므로, 트랜스폼을 몸 자리에 도로 맞춘다. 물리 스텝 전이라 보간(Interpolate)도 깨지지 않는다.
        void MoveChainRoot(Vector2 to)
        {
            chainRoot.position = to;
            PinToBody(this);
            for (int i = 0; i < followers.Count; i++) PinToBody(followers[i]);
        }

        static void PinToBody(EnemySerpent segment) => segment.transform.position = segment.Body.position;

        // 이 머리와 몸통을 새 루트 아래 묶고 그룹 안 순서를 매긴다. 스폰 직후와 승격 직후에 부른다.
        void BuildChain()
        {
            var root = new GameObject($"{name} Chain");
            root.transform.position = Body.position;
            var group = root.AddComponent<SortingGroup>();
            group.sortingLayerID = Sprite.sortingLayerID;
            group.sortingOrder = groupSortingOrder;
            chainRoot = root.transform;
            Attach(this);
            for (int i = 0; i < followers.Count; i++) Attach(followers[i]);
            RefreshSortingOrders();
        }

        // 마디를 이 사슬의 루트 아래로 옮긴다. 자리는 몸이 갖고 있으므로 트랜스폼을 몸 자리에 맞춘다 (보간 중이던 값이 몸에 되쓰이지 않게).
        void Attach(EnemySerpent segment)
        {
            segment.transform.SetParent(chainRoot, true);
            PinToBody(segment);
        }

        // 그룹 안의 순서: 머리 0, 몸통은 뒤로 갈수록 한 단계씩 아래. 앞 마디가 늘 뒤 마디 위에 온다.
        void RefreshSortingOrders()
        {
            Sprite.sortingOrder = 0;
            for (int i = 0; i < followers.Count; i++)
                followers[i].Sprite.sortingOrder = -(i + 1);
        }

        // 머리 owner의 몸통이 된다. 조향을 멈추고 자리는 머리가 정한다.
        void BecomeBody(EnemySerpent owner)
        {
            head = owner;
            groupSortingOrder = owner.groupSortingOrder;
            followers = null;
            trail = null;
            chainRoot = null;
            enabled = false;
            walkCycle.enabled = false;
            Sprite.sprite = bodySprite;
            Sprite.flipX = false;
        }

        // 몸통이 머리가 된다. 넘겨받은 몸통·궤적으로 이어서 나아가고, 자기 사슬 루트를 새로 만든다. HP는 그대로.
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
            BuildChain();
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

        // 머리가 사라지면 사슬 루트도 치운다. Died를 거친 죽음이면 몸통은 이미 새 머리의 루트로 옮겨 가 있다.
        void OnDestroy()
        {
            if (head == this && chainRoot != null) Destroy(chainRoot.gameObject);
        }
    }
}
