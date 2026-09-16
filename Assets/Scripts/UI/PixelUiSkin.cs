using UnityEngine;

namespace PickShoot
{
    // IMGUI로 그리는 픽셀 UI(업그레이드 카드 등)가 쓰는 스프라이트 묶음. Kenney Interface 시트의 조각을 참조한다.
    // 이 자산은 "어느 스프라이트를 쓰는가"만 보관하고, 그리기는 PixelGui가 담당한다.
    [CreateAssetMenu(menuName = "PickShoot/Pixel UI Skin", fileName = "PixelUiSkin")]
    public class PixelUiSkin : ScriptableObject
    {
        // 9-slice 패널. 순서는 시트 격자 그대로: 좌상, 상, 우상, 좌, 중앙, 우, 좌하, 하, 우하.
        [System.Serializable]
        public class NineSlice
        {
            public Sprite[] tiles = new Sprite[9];
        }

        // 시트에 그려진 비트맵 글꼴. glyphs[i]가 characters[i]의 스프라이트.
        // glyphLeft/glyphWidth는 타일 안에서 실제 픽셀이 있는 가로 구간(텍셀) — 가변폭 배치용.
        [System.Serializable]
        public class BitmapFont
        {
            public string characters = "";
            public Sprite[] glyphs = new Sprite[0];
            public int[] glyphLeft = new int[0];
            public int[] glyphWidth = new int[0];
            public int lineHeight = 16;
            public int letterSpacing = 1;
            public int spaceWidth = 5;
        }

        [SerializeField, Min(1)] int tileSize = 16;
        [SerializeField] NineSlice panelBlue;
        [SerializeField] NineSlice panelYellow;
        [SerializeField] NineSlice panelRed;
        [SerializeField] NineSlice panelGrey;
        // 투명 중앙 + 흰 모서리 괄호. 다른 패널 위에 겹쳐 그리는 선택(hover) 표시.
        [SerializeField] NineSlice selectionFrame;
        [SerializeField] BitmapFont fontOutlined;
        [SerializeField] BitmapFont fontPlain;
        [SerializeField] Sprite iconBullet;
        [SerializeField] Sprite iconChevronUp;
        [SerializeField] Sprite iconArrowUp;

        public int TileSize => tileSize;
        public NineSlice PanelBlue => panelBlue;
        public NineSlice PanelYellow => panelYellow;
        public NineSlice PanelRed => panelRed;
        public NineSlice PanelGrey => panelGrey;
        public NineSlice SelectionFrame => selectionFrame;
        public BitmapFont FontOutlined => fontOutlined;
        public BitmapFont FontPlain => fontPlain;
        public Sprite IconBullet => iconBullet;
        public Sprite IconChevronUp => iconChevronUp;
        public Sprite IconArrowUp => iconArrowUp;

        // 누락된 참조를 필드 이름까지 지목해 예외로 알린다. 사용하는 쪽이 Awake에서 호출.
        public void Validate()
        {
            ValidateNineSlice(panelBlue, nameof(panelBlue));
            ValidateNineSlice(panelYellow, nameof(panelYellow));
            ValidateNineSlice(panelRed, nameof(panelRed));
            ValidateNineSlice(panelGrey, nameof(panelGrey));
            ValidateNineSlice(selectionFrame, nameof(selectionFrame));
            ValidateFont(fontOutlined, nameof(fontOutlined));
            ValidateFont(fontPlain, nameof(fontPlain));
            if (iconBullet == null) throw Missing(nameof(iconBullet));
            if (iconChevronUp == null) throw Missing(nameof(iconChevronUp));
            if (iconArrowUp == null) throw Missing(nameof(iconArrowUp));
        }

        System.Exception Missing(string field)
        {
            return new System.Exception($"[PickShoot] PixelUiSkin '{name}'.{field} 미할당.");
        }

        void ValidateNineSlice(NineSlice slice, string field)
        {
            if (slice == null || slice.tiles == null || slice.tiles.Length != 9)
                throw new System.Exception($"[PickShoot] PixelUiSkin '{name}'.{field}: 타일이 정확히 9개여야 함.");
            for (int i = 0; i < 9; i++)
                if (slice.tiles[i] == null)
                    throw Missing($"{field}.tiles[{i}]");
        }

        void ValidateFont(BitmapFont font, string field)
        {
            if (font == null || string.IsNullOrEmpty(font.characters))
                throw Missing(field);
            int n = font.characters.Length;
            if (font.glyphs == null || font.glyphs.Length != n
                || font.glyphLeft == null || font.glyphLeft.Length != n
                || font.glyphWidth == null || font.glyphWidth.Length != n)
                throw new System.Exception($"[PickShoot] PixelUiSkin '{name}'.{field}: characters({n})와 glyphs/glyphLeft/glyphWidth 길이가 다름.");
            for (int i = 0; i < n; i++)
            {
                if (font.glyphs[i] == null)
                    throw Missing($"{field}.glyphs[{i}] ('{font.characters[i]}')");
                if (font.glyphWidth[i] <= 0)
                    throw new System.Exception($"[PickShoot] PixelUiSkin '{name}'.{field}.glyphWidth[{i}] ('{font.characters[i]}')가 0 이하.");
            }
        }
    }
}
