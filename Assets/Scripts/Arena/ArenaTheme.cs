using UnityEngine;

namespace PickShoot
{
    // 아레나를 꾸미는 타일 스프라이트 묶음. Kenney Tiles 시트의 조각을 참조한다.
    // 이 자산은 "어느 스프라이트를 쓰는가"만 보관하고, 배치는 ArenaDecor가 담당한다.
    [CreateAssetMenu(menuName = "PickShoot/Arena Theme", fileName = "ArenaTheme")]
    public class ArenaTheme : ScriptableObject
    {
        // 땅 타일과, 드문드문 섞는 변형. 울타리 안팎 구분 없이 같은 땅을 쓴다.
        // 탄환이 잘 보여야 하므로 변형은 바탕과 같은 색의 잔무늬만 쓴다. 같은 변형을 여러 번 넣어 등장 비중을 조절한다.
        [SerializeField] Sprite floor;
        [SerializeField] Sprite[] floorVariants;

        // 경계 울타리. 한 칸 두께의 고리로 두른다: 네 모서리 기둥, 좌우 세로 울타리,
        // 위·아래 가로 레일(기둥 없는 칸 fenceRail, 기둥 있는 칸 fenceRailPost — 둘은 이음새가 같아 섞어 쓴다).
        [SerializeField] Sprite fenceTopLeft;
        [SerializeField] Sprite fenceTopRight;
        [SerializeField] Sprite fenceBottomLeft;
        [SerializeField] Sprite fenceBottomRight;
        [SerializeField] Sprite fenceLeft;
        [SerializeField] Sprite fenceRight;
        [SerializeField] Sprite fenceRail;
        [SerializeField] Sprite fenceRailPost;

        // 위쪽 울타리가 바닥에 드리우는 그림자 띠.
        [SerializeField] Color fenceShadow = new Color(0.12f, 0.06f, 0.16f, 0.25f);

        public Sprite Floor => floor;
        public Sprite[] FloorVariants => floorVariants;
        public Sprite FenceTopLeft => fenceTopLeft;
        public Sprite FenceTopRight => fenceTopRight;
        public Sprite FenceBottomLeft => fenceBottomLeft;
        public Sprite FenceBottomRight => fenceBottomRight;
        public Sprite FenceLeft => fenceLeft;
        public Sprite FenceRight => fenceRight;
        public Sprite FenceRail => fenceRail;
        public Sprite FenceRailPost => fenceRailPost;
        public Color FenceShadow => fenceShadow;

        // 누락된 참조를 필드 이름까지 지목해 예외로 알린다. 사용하는 쪽이 Awake에서 호출.
        public void Validate()
        {
            if (floor == null) throw Missing(nameof(floor));
            ValidateSprites(floorVariants, nameof(floorVariants));
            if (fenceTopLeft == null) throw Missing(nameof(fenceTopLeft));
            if (fenceTopRight == null) throw Missing(nameof(fenceTopRight));
            if (fenceBottomLeft == null) throw Missing(nameof(fenceBottomLeft));
            if (fenceBottomRight == null) throw Missing(nameof(fenceBottomRight));
            if (fenceLeft == null) throw Missing(nameof(fenceLeft));
            if (fenceRight == null) throw Missing(nameof(fenceRight));
            if (fenceRail == null) throw Missing(nameof(fenceRail));
            if (fenceRailPost == null) throw Missing(nameof(fenceRailPost));
        }

        System.Exception Missing(string field)
        {
            return new System.Exception($"[PickShoot] ArenaTheme '{name}'.{field} 미할당.");
        }

        void ValidateSprites(Sprite[] sprites, string field)
        {
            if (sprites == null) throw Missing(field);
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] == null) throw Missing($"{field}[{i}]");
        }
    }
}
