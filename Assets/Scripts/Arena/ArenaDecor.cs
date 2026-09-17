using UnityEngine;

namespace PickShoot
{
    // 아레나의 겉모습(땅·경계 울타리)을 시작 시 타일 스프라이트로 조립한다.
    // 순수 장식이다 — 충돌은 씬의 벽 콜라이더가 맡고, 여기서 만든 오브젝트에는 콜라이더가 없다.
    // 한 칸 = 1 월드 유닛(16텍셀)이고 칸 (x, y)는 [x, x+1]×[y, y+1]을 덮는다. 아레나 안쪽은 -arenaHalfCells ~ arenaHalfCells-1.
    // 땅은 울타리 안팎 구분 없이 한 장이고, 꾸밈은 바탕과 같은 색조의 잔무늬 타일을 드문드문 섞는 것뿐이다.
    // 얼룩이나 서 있는 소품은 두지 않는다.
    public class ArenaDecor : MonoBehaviour
    {
        [SerializeField] ArenaTheme theme;
        // 울타리는 이 바깥 한 칸에 두른다. 씬의 벽 콜라이더 안쪽 면은 울타리 그림이 실제로 보이는 면에 맞춰 둔다
        // (어긋나면 튕김 위치가 거짓말이 된다): 위는 칸 경계, 아래는 레일 윗선이라 2텍셀 아래,
        // 좌우는 세로 울타리가 칸 중앙의 8텍셀 띠라 4텍셀 바깥.
        [SerializeField, Min(1)] int arenaHalfCells = 14;
        // 땅을 까는 범위. 가로로 긴 화면과 카메라 흔들림까지 덮도록 시야보다 넉넉히 잡는다.
        [SerializeField] Vector2Int outerHalfCells = new Vector2Int(28, 17);
        // 같은 시드는 항상 같은 잔무늬 배치를 만든다.
        [SerializeField] uint seed = 3;
        // 변형 타일이 섞이는 빈도: 칸 variantRarity개에 하나꼴.
        [SerializeField, Min(1)] int variantRarity = 12;

        // 정렬 순서. 적(3)·탄환(4)·플레이어(5)보다 아래에 둔다 — 울타리에 붙은 탄환도 끝까지 보인다.
        const int GroundOrder = -10;
        const int GroundDetailOrder = -9;
        const int ShadowOrder = -8;
        const int FenceOrder = 1;

        const float ShadowHeight = 4f / 16f;
        const int FencePostSpacing = 4;

        // 정수 연산만 쓰는 xorshift32. UnityEngine.Random을 시드로 초기화하면 전역 상태가 고정돼
        // 적 스폰까지 매 판 같아지므로 전용 난수기를 쓴다.
        class CellRandom
        {
            uint state;

            public CellRandom(uint seed) { state = seed == 0 ? 1u : seed; }

            uint Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            // [min, max)
            public int Range(int min, int max) => min + (int)(Next() % (uint)(max - min));
            public bool OneIn(int n) => Next() % (uint)n == 0;
        }

        Transform root;

        void Awake()
        {
            if (theme == null)
                throw new System.Exception("[PickShoot] ArenaDecor.theme 미설정.");
            theme.Validate();
            int fenceOuter = arenaHalfCells + 1;
            if (outerHalfCells.x < fenceOuter || outerHalfCells.y < fenceOuter)
                throw new System.Exception($"[PickShoot] ArenaDecor.outerHalfCells {outerHalfCells}가 울타리 바깥 면({fenceOuter})보다 작음.");

            root = new GameObject("Decor").transform;
            root.SetParent(transform, false);

            BuildGround();
            BuildFence();
        }

        void BuildGround()
        {
            int a = arenaHalfCells;
            int ox = outerHalfCells.x;
            int oy = outerHalfCells.y;

            // 바탕 한 장: 원점 중심으로 땅 타일을 반복해 깐다.
            var ground = NewRenderer("Ground", theme.Floor, GroundOrder, Color.white);
            ground.drawMode = SpriteDrawMode.Tiled;
            ground.size = new Vector2(2 * ox, 2 * oy);

            ScatterVariants();

            // 위쪽 울타리 바로 아래에 그림자 띠를 깔아 울타리가 서 있는 것처럼 보이게 한다. 양 끝은 세로 울타리 밑에 가린다.
            var white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            var shadow = NewRenderer("FenceShadow", white, ShadowOrder, theme.FenceShadow).transform;
            shadow.localPosition = new Vector3(0f, a - ShadowHeight * 0.5f, 0f);
            shadow.localScale = new Vector3(2 * a + 1, ShadowHeight, 1f);
        }

        // 칸마다 variantRarity분의 1 확률로 변형 타일을 덧놓는다. 울타리 칸은 건너뛴다 — 울타리 틈으로 무늬가 잘려 보인다.
        void ScatterVariants()
        {
            var variants = theme.FloorVariants;
            if (variants.Length == 0) return;
            var rng = new CellRandom(seed);
            for (int y = -outerHalfCells.y; y < outerHalfCells.y; y++)
            {
                for (int x = -outerHalfCells.x; x < outerHalfCells.x; x++)
                {
                    if (IsFenceCell(x, y)) continue;
                    if (rng.OneIn(variantRarity))
                        Put(variants[rng.Range(0, variants.Length)], x, y, GroundDetailOrder);
                }
            }
        }

        // 울타리 고리(아레나 바깥 한 칸)에 속하는 칸인지.
        bool IsFenceCell(int x, int y)
        {
            int a = arenaHalfCells;
            if (x < -a - 1 || x > a || y < -a - 1 || y > a) return false;
            return x == -a - 1 || x == a || y == -a - 1 || y == a;
        }

        // 아레나 바깥 한 칸에 울타리 고리를 두른다. 가로 레일의 기둥은 중앙에서 좌우 대칭으로
        // FencePostSpacing칸마다 세운다 (칸 중심 x = ±2.5, ±6.5, …).
        void BuildFence()
        {
            int a = arenaHalfCells;
            for (int x = -a; x < a; x++)
            {
                bool post = Mathf.Abs(2 * x + 1) % (2 * FencePostSpacing) == FencePostSpacing + 1;
                var rail = post ? theme.FenceRailPost : theme.FenceRail;
                Put(rail, x, a, FenceOrder);
                Put(rail, x, -a - 1, FenceOrder);
            }
            for (int y = -a; y < a; y++)
            {
                Put(theme.FenceLeft, -a - 1, y, FenceOrder);
                Put(theme.FenceRight, a, y, FenceOrder);
            }
            Put(theme.FenceTopLeft, -a - 1, a, FenceOrder);
            Put(theme.FenceTopRight, a, a, FenceOrder);
            Put(theme.FenceBottomLeft, -a - 1, -a - 1, FenceOrder);
            Put(theme.FenceBottomRight, a, -a - 1, FenceOrder);
        }

        void Put(Sprite sprite, int cellX, int cellY, int order)
        {
            var sr = NewRenderer(sprite.name, sprite, order, Color.white);
            sr.transform.localPosition = new Vector3(cellX + 0.5f, cellY + 0.5f, 0f);
        }

        SpriteRenderer NewRenderer(string label, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }
    }
}
