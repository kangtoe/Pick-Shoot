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

        // 가로 바(진행 바 프레임·채움) 조각: 왼쪽 끝, 가운데(반복), 오른쪽 끝.
        [System.Serializable]
        public class BarStrip
        {
            public Sprite left;
            public Sprite mid;
            public Sprite right;
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
            // 대문자·숫자 글리프의 어두운 상자가 칸(lineHeight) 안에서 차지하는 세로 구간(텍셀) — 위 여백과 높이. 배지 바탕(ItemTilePainter)이 글자에 꼭 맞게 쓴다.
            // Q(꼬리)·구두점은 구간이 다르지만 배지 문구는 대문자·숫자뿐이다.
            public int capTop = 2;
            public int capHeight = 12;
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
        // 진행 바: 흰 테두리 프레임과, 같은 자리에 겹쳐 그리면 안쪽에 맞는 채움 — 붉은색(체력·웨이브 남은 시간)과 파란색(경험치).
        [SerializeField] BarStrip barFrame;
        [SerializeField] BarStrip barFillRed;
        [SerializeField] BarStrip barFillBlue;
        // 테두리 없는 작은 탄환(탄약 핍)과 붉은 X(탄약 부족 표시).
        [SerializeField] Sprite iconBulletPlain;
        [SerializeField] Sprite iconCross;
        // 해골. 데스 화면 배너 장식용.
        [SerializeField] Sprite iconSkull;
        // 무기 스프라이트 밑의 작은 판(HUD 무기 줄·빌드 띠, PixelGui.DrawWeaponPlate) 색: 시트 외곽선 색 테두리 + 회색 패널 바탕색 (DEVELOPMENT §2).
        [SerializeField] Color weaponPlateFill = new Color32(153, 154, 196, 255);
        [SerializeField] Color weaponPlateOutline = new Color32(71, 50, 75, 255);
        // 강조 글자색 — 업그레이드 카드의 `NEW` 제목(새 무기·유물, UpgradeCardPainter). XP 팝업(PlayerExperience.popupColor)과 같은 노랑으로 맞춘다 (2026-10-08).
        [SerializeField] Color highlightText = new Color32(255, 184, 76, 255);
        // 업그레이드 카드의 `UPGRADE` 제목(무기 레벨업) 글자색 — Pixel Platformer 팩의 밝은 초록. 사막 팩에는 초록이 없고 가장 가까운 민트(재생 문구색)는 청록으로
        // 읽혔으며, 더 짙은 초록은 노란 `NEW`보다 어두워 보였다 (2026-10-09, Recordings/card_upgrade_green_mockup_2026-10-09.png).
        [SerializeField] Color upgradeText = new Color32(127, 240, 165, 255);
        // 개조 카드의 `MOD` 제목과 빌드 타일 배지의 개조 머리글자(`LV5H`의 `H`) 글자색 — Pixel Platformer 팩의 분홍. 노란 `NEW`·초록 `UPGRADE`·파란 카드
        // 패널과 가장 잘 갈렸다. 청록은 다음으로 읽혔고, 살구색은 `NEW`와, 라벤더는 패널과 묻혔다 (2026-10-10, Recordings/mod_card_color_mockup_2026-10-10.png).
        [SerializeField] Color modText = new Color32(255, 154, 186, 255);

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
        public BarStrip BarFrame => barFrame;
        public BarStrip BarFillRed => barFillRed;
        public BarStrip BarFillBlue => barFillBlue;
        public Sprite IconBulletPlain => iconBulletPlain;
        public Sprite IconCross => iconCross;
        public Sprite IconSkull => iconSkull;
        public Color WeaponPlateFill => weaponPlateFill;
        public Color WeaponPlateOutline => weaponPlateOutline;
        public Color HighlightText => highlightText;
        public Color UpgradeText => upgradeText;
        public Color ModText => modText;

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
            ValidateBarStrip(barFrame, nameof(barFrame));
            ValidateBarStrip(barFillRed, nameof(barFillRed));
            ValidateBarStrip(barFillBlue, nameof(barFillBlue));
            if (iconBulletPlain == null) throw Missing(nameof(iconBulletPlain));
            if (iconCross == null) throw Missing(nameof(iconCross));
            if (iconSkull == null) throw Missing(nameof(iconSkull));
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

        void ValidateBarStrip(BarStrip strip, string field)
        {
            if (strip == null) throw Missing(field);
            if (strip.left == null) throw Missing($"{field}.left");
            if (strip.mid == null) throw Missing($"{field}.mid");
            if (strip.right == null) throw Missing($"{field}.right");
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
            if (font.capTop < 0 || font.capHeight <= 0 || font.capTop + font.capHeight > font.lineHeight)
                throw new System.Exception($"[PickShoot] PixelUiSkin '{name}'.{field}: capTop {font.capTop}·capHeight {font.capHeight}가 lineHeight {font.lineHeight} 안에 있지 않음.");
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
