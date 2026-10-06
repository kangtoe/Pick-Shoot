using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // ESC로 게임을 멈추고 화면을 어둡게 덮는 일시정지 화면. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    // 구성은 업그레이드 카드와 같은 결의 상태 카드 한 장: 회색 카드(패널 배율) 위쪽 끝에 파란 "PAUSED" 배지(카드 배지와 같은 그리기, UpgradeCardPainter.DrawBadge)가
    // 걸치고, 안에는 WAVE·LEVEL·XP 표 — 라벨은 왼쪽(일반 글꼴), 값은 오른쪽(아웃라인 글꼴), 글자 배율. 줄이 카드 폭을 끝까지 쓰므로 카드 크기는 줄 수에서 정해진다.
    // 재개 안내는 카드 밖 아래. 배너 + 상자를 쌓는 데스 화면 꼴은 짧은 두 줄로는 상자가 배너와 겨루는 두 번째 덩어리로 보여 뺐다 (2026-10-02).
    // 일시정지 중엔 HUD가 숨으므로 웨이브도 여기서 보이고, 누적 경험치 총량은 여기서만 보인다 (GDD §3.3).
    // 상태 카드 아래에 현재 빌드 격자(BuildGridPainter — 제목 `WEAPONS (7/12)` 아래 보유 무기 한 줄, 제목 `RELICS (5)` 아래 유물 격자, 패널 배율 타일)를 둔다
    // (2026-10-04 두 줄 띠 → 2026-10-06 타일 격자·섹션 제목).
    // 타일에 마우스를 올리면 툴팁(TooltipPainter)이 뜬다 — 하드웨어 커서는 멈춘 동안 GameHud가 되살린다.
    // 멈춤은 업그레이드 카드와 같은 방식(Time.timeScale = 0)이라 물리·코루틴·사격이 함께 멎는다.
    // 카드 선택 중(이미 멈춰 있음)과 플레이어 사망 뒤(데스 화면이 이어받음)에는 ESC를 받지 않는다.
    // 멈춘 동안 HUD는 GameHud가 걷고 하드웨어 커서를 되살린다 — 볼륨 조절 등 클릭 조작이 들어올 자리.
    // OnGUI가 PlayerHealth의 데미지 플래시 위에 그려지도록 실행 순서를 뒤로 민다.
    [DefaultExecutionOrder(100)]
    public class PauseScreen : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] UpgradeManager upgradeManager;
        [SerializeField] PlayerExperience experience;
        [SerializeField] WaveManager waveManager;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        const string Title = "PAUSED";
        const string Hint = "PRESS ESC TO RESUME";
        static readonly string[] RowLabels = { "WAVE", "LEVEL", "XP" };
        const int CardWidthTiles = 7;        // 업그레이드 카드와 같은 폭
        const int SidePaddingTexels = 10;    // 카드 테두리 → 라벨·값 (패널 텍셀). 카드의 좌우 여백과 같다.
        const int TablePaddingTexels = 10;   // 배지 아래 가장자리 → 첫 줄, 마지막 줄 → 카드 아래 테두리의 최소 여백 (패널 텍셀). 타일 올림의 여분은 위아래 균등.
        const int RowGapTexels = 4;          // 줄 사이 (글자 텍셀)
        const int StripGapTexels = 8;        // 카드 아래 → 빌드 격자 (글자 텍셀)
        const int HintGapTexels = 16;        // 빌드 격자 아래 → 재개 안내 (글자 텍셀)
        const int SectionGapTexels = 12;     // 무기 줄 아래 → 유물 제목 (글자 텍셀). 제목과 그 타일 사이(BuildGridPainter, 4)보다 넓어 섹션이 갈린다.

        bool paused;
        float prevTimeScale;
        // 빌드 격자의 출처. 플레이어 오브젝트의 컴포넌트라 playerHealth에서 함께 찾는다.
        PlayerWeapons weapons;
        PlayerStats stats;
        // 툴팁 내용 버퍼 (프레임마다 비우고 채운다).
        readonly List<string> tipFacts = new List<string>();
        readonly List<string> tipNotes = new List<string>();

        public bool IsPaused => paused;

        void Awake()
        {
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] PauseScreen.playerHealth 미할당 (사망 뒤 일시정지 차단에 필요).");
            if (upgradeManager == null)
                throw new System.Exception("[PickShoot] PauseScreen.upgradeManager 미할당 (카드 선택 중 일시정지 차단에 필요).");
            if (experience == null)
                throw new System.Exception("[PickShoot] PauseScreen.experience 미할당 (레벨·누적 경험치 표기에 필요).");
            if (waveManager == null)
                throw new System.Exception("[PickShoot] PauseScreen.waveManager 미할당 (웨이브 표기에 필요).");
            if (skin == null)
                throw new System.Exception("[PickShoot] PauseScreen.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            weapons = playerHealth.GetComponent<PlayerWeapons>();
            if (weapons == null)
                throw new System.Exception("[PickShoot] PauseScreen: 플레이어에 PlayerWeapons 없음 (빌드 격자의 무기 줄).");
            stats = playerHealth.GetComponent<PlayerStats>();
            if (stats == null)
                throw new System.Exception("[PickShoot] PauseScreen: 플레이어에 PlayerStats 없음 (빌드 격자의 유물 격자).");
        }

        // 멈춘 채 파괴되면(씬 재시작 등) timeScale이 0으로 남아 다음 씬까지 멎으므로 되돌린다.
        void OnDestroy()
        {
            if (paused) Resume();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.escapeKey.wasPressedThisFrame) return;

            if (paused)
            {
                Resume();
                return;
            }
            // 플레이어는 사망 시 파괴된다. 그 뒤는 데스 화면이, 카드 선택 중은 UpgradeManager가 화면을 맡는다.
            if (playerHealth == null || upgradeManager.IsActive) return;
            Pause();
        }

        void Pause()
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
        }

        void Resume()
        {
            Time.timeScale = prevTimeScale;
            paused = false;
        }

        void OnGUI()
        {
            if (!paused) return;
            if (Event.current.type != EventType.Repaint) return;

            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int tile = skin.TileSize * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전).
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            using var canvas = PixelGui.BeginCanvas();

            int rowGap = RowGapTexels * textScale;
            int tableH = RowLabels.Length * lineH + (RowLabels.Length - 1) * rowGap;
            // 카드 높이 = 배지가 들어온 깊이 + 표 + 위아래 최소 여백, 타일 배수로 올림. 배지가 위로 솟은 만큼까지 묶음에 넣어 세로 중앙에 놓는다.
            int inset = UpgradeCardPainter.BadgeInsetTexels * scale;
            int cardW = CardWidthTiles * tile;
            int cardH = CeilToMultiple(inset + tableH + 2 * TablePaddingTexels * scale, tile);
            int overhang = UpgradeCardPainter.BadgeOverhang(skin, scale);
            int stripGap = StripGapTexels * textScale;
            int gridScale = scale;
            BuildGridPainter.RequireFits(gridScale, canvas.Width);
            int relicRows = BuildGridPainter.RelicRows(BuildGridPainter.RelicKinds(stats).Count);
            // 격자 묶음 = 제목 줄 + 무기 줄 + 섹션 간격 + 제목 줄 + 유물 격자.
            int titleH = BuildGridPainter.TitleHeight(skin);
            int tileH = ItemTilePainter.Size(gridScale);
            int sectionGap = SectionGapTexels * textScale;
            int gridH = titleH + tileH + sectionGap + titleH + BuildGridPainter.Height(skin, gridScale, relicRows);
            int hintGap = HintGapTexels * textScale;
            // 묶음 = 배지 돌출 + 카드 + 빌드 격자(무기 한 줄 + 유물 줄들) + 재개 안내. 세로 중앙에.
            int blockH = overhang + cardH + stripGap + gridH + hintGap + lineH;
            if (blockH > canvas.Height)
                throw new System.Exception($"[PickShoot] PauseScreen: 묶음 높이 {blockH}px가 캔버스 {canvas.Height}px를 넘음 (유물 {relicRows}줄). 격자 배율을 낮추거나 줄을 줄일 것.");
            int centerX = canvas.Width / 2;

            var card = new Rect(centerX - cardW / 2, (canvas.Height - blockH) / 2 + overhang, cardW, cardH);
            PixelGui.DrawNineSlice(skin.PanelGrey, card, skin.TileSize, scale);
            UpgradeCardPainter.DrawBadge(skin, card, scale, Title, skin.PanelBlue);

            // 표는 배지 아래 남은 높이의 세로 가운데. 라벨은 왼쪽 정렬, 값은 오른쪽 정렬.
            int sidePad = SidePaddingTexels * scale;
            int labelX = (int)card.x + sidePad;
            int valueRight = (int)card.xMax - sidePad;
            int y = (int)card.y + inset + (cardH - inset - tableH) / 2;
            for (int i = 0; i < RowLabels.Length; i++)
            {
                PixelGui.DrawText(skin.FontPlain, RowLabels[i], labelX, y, textScale);
                PixelGui.DrawTextRight(skin.FontOutlined, RowValue(i), valueRight, y, textScale);
                y += lineH + rowGap;
            }

            int gridX = centerX - BuildGridPainter.Width(gridScale) / 2;
            int gridY = (int)card.yMax + stripGap;
            var mouse = Event.current.mousePosition;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.WeaponTitle(weapons), gridX, gridY);
            int weaponY = gridY + titleH;
            int slot = BuildGridPainter.DrawWeaponRow(skin, weapons, gridX, weaponY, gridScale, mouse, out var weaponTile);
            int relicTitleY = weaponY + tileH + sectionGap;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.RelicTitle(stats), gridX, relicTitleY);
            var relic = BuildGridPainter.DrawRelicGrid(skin, stats, gridX, relicTitleY + titleH, gridScale, relicRows, mouse, out var relicTile);
            PixelGui.DrawTextCentered(skin.FontPlain, Hint, centerX, gridY + gridH + hintGap, textScale);

            // 툴팁은 다 그린 뒤 맨 마지막에 — 이웃 타일 위로 뜬다.
            if (slot >= 0)
            {
                var w = weapons.Slots[slot];
                tipFacts.Clear();
                tipNotes.Clear();
                ItemInfoText.WeaponFacts(w.Data, w.Level, slot, stats, tipFacts);
                ItemInfoText.WeaponNotes(w.Data, tipNotes);
                TooltipPainter.Draw(skin, canvas, weaponTile, ItemInfoText.WeaponTitle(w.Data), tipFacts, tipNotes);
            }
            else if (relic != null)
            {
                tipFacts.Clear();
                tipNotes.Clear();
                ItemInfoText.RelicFacts(relic, stats.CountOf(relic), tipFacts);
                ItemInfoText.RelicNotes(relic, tipNotes);
                TooltipPainter.Draw(skin, canvas, relicTile, ItemInfoText.RelicTitle(relic), tipFacts, tipNotes);
            }
        }

        // RowLabels와 같은 순서의 값.
        string RowValue(int row)
        {
            switch (row)
            {
                case 0: return waveManager.CurrentWave.ToString();
                case 1: return experience.Level.ToString();
                case 2: return experience.Total.ToString();
                default: throw new System.Exception($"[PickShoot] PauseScreen: 표에 없는 줄 {row}.");
            }
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
